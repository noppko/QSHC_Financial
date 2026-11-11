using Financial.Models.Dashboard;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Reflection;

namespace Financial.Services.Dashboard
{
    /// <summary>
    /// Dashboard การเงินจาก HIS HealthObject (ConnectionStrings:HealthObject)
    /// - ใช้เฉพาะ SELECT จากไฟล์ Database/his/queries/*.sql (ฝังใน assembly) — ไฟล์เดียวกันใช้ตรวจด้วย tools/query.mjs ได้
    /// - ข้อมูลย้อนหลังเปลี่ยนน้อย → cache 30 นาที, ข้อมูลวันนี้/ผู้ป่วยรอชำระ → cache 60 วินาที (หลายคนเปิดพร้อมกัน query ครั้งเดียว)
    /// - ส่วนใดล้มเหลว จะแสดงส่วนอื่นต่อได้ และแจ้งใน Warnings
    /// </summary>
    public class HisFinanceDashboardService : IFinanceDashboardService
    {
        private static readonly TimeSpan HistoryCache = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan LiveCache = TimeSpan.FromSeconds(60);

        private readonly string? _connectionString;
        private readonly int _commandTimeoutSeconds;
        private readonly IMemoryCache _cache;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<HisFinanceDashboardService> _logger;

        public HisFinanceDashboardService(IConfiguration configuration, IMemoryCache cache, TimeProvider timeProvider, ILogger<HisFinanceDashboardService> logger)
        {
            _connectionString = configuration.GetConnectionString("HealthObject");
            _commandTimeoutSeconds = configuration.GetValue("Dashboard:CommandTimeoutSeconds", 60);
            _cache = cache;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task<FinanceDashboard> GetAsync(bool includePatientIdentity = false, CancellationToken cancellationToken = default)
        {
            var now = ThaiClock.Now(_timeProvider);
            var today = now.Date;
            var tomorrow = today.AddDays(1);
            var monthStart = new DateTime(today.Year, today.Month, 1);
            var previousMonthStart = monthStart.AddMonths(-1);
            var historyFrom = previousMonthStart < today.AddDays(-29) ? previousMonthStart : today.AddDays(-29);

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                return new FinanceDashboard { GeneratedAt = now, DataSource = "HIS", Warnings = ["ยังไม่ได้ตั้งค่า ConnectionStrings:HealthObject"] };
            }

            var warnings = new List<string>();

            // ทุกส่วนเริ่มพร้อมกัน (แยก connection) — เวลารวม ≈ query ที่ช้าที่สุด
            var historyTask = Section("รายได้ย้อนหลัง", warnings, () => Cached($"his:rev:{historyFrom:yyyyMMdd}:{today:yyyyMMdd}", HistoryCache,
                ct => QueryAsync(HisQueries.RevenueDaily, ReadRevenueRows, ct, ("@from", historyFrom), ("@to", today)), cancellationToken));
            var todayTask = Section("รายได้วันนี้", warnings, () => Cached($"his:rev:{today:yyyyMMdd}:live", LiveCache,
                ct => QueryAsync(HisQueries.RevenueDaily, ReadRevenueRows, ct, ("@from", today), ("@to", tomorrow)), cancellationToken));
            var collectionsTask = Section("เงินรับวันนี้", warnings, () => Cached($"his:collect:{today:yyyyMMdd}", LiveCache,
                ct => QueryAsync(HisQueries.Collections, ReadCollections, ct, ("@from", today), ("@to", tomorrow)), cancellationToken));
            var volumeTask = Section("ปริมาณผู้ป่วย", warnings, () => Cached($"his:volume:{today:yyyyMMdd}", LiveCache,
                ct => QueryAsync(HisQueries.Volume, ReadVolume, ct, ("@today", today), ("@tomorrow", tomorrow)), cancellationToken));
            var pendingOpdTask = Section("ผู้ป่วยนอกรอชำระ", warnings, () => Cached("his:pending:O", LiveCache,
                ct => QueryAsync(HisQueries.PendingOpd, r => ReadPending(r, "O", now), ct, ("@today", today), ("@tomorrow", tomorrow), ("@since", now.AddHours(-8))), cancellationToken));
            var pendingIpdTask = Section("ผู้ป่วยในรอชำระ", warnings, () => Cached("his:pending:I", LiveCache,
                ct => QueryAsync(HisQueries.PendingIpd, r => ReadPending(r, "I", now), ct), cancellationToken));

            await Task.WhenAll(historyTask, todayTask, collectionsTask, volumeTask, pendingOpdTask, pendingIpdTask);

            var revenueRows = (historyTask.Result ?? []).Concat(todayTask.Result ?? []).ToList();
            var pending = (pendingOpdTask.Result ?? []).Concat(pendingIpdTask.Result ?? []).ToList();
            if (includePatientIdentity && pending.Count > 0)
            {
                pending = await AddIdentityAsync(pending, warnings, cancellationToken);
            }

            return FinanceDashboardBuilder.Build(now, revenueRows, collectionsTask.Result ?? [], volumeTask.Result, pending, warnings);
        }

        /// <summary>
        /// เติม HN/ชื่อ-สกุล (เฉพาะ Admin) — query เฉพาะ visit ที่ยังไม่มีใน cache, เก็บ 5 นาที
        /// ผู้ป่วยปกปิดชื่อ (IsAnonymous) แสดงเป็น "ผู้ป่วยปกปิดชื่อ" และไม่แสดง HN
        /// </summary>
        private async Task<List<PendingPayment>> AddIdentityAsync(List<PendingPayment> pending, List<string> warnings, CancellationToken cancellationToken)
        {
            var missing = pending.Select(p => p.VisitUid).Where(u => u != 0 && !_cache.TryGetValue(IdentityKey(u), out PatientIdentity? _)).Distinct().ToList();
            if (missing.Count > 0)
            {
                try
                {
                    foreach (var chunk in missing.Chunk(500))
                    {
                        var rows = await QueryIdentityAsync(chunk, cancellationToken);
                        foreach (var uid in chunk)
                        {
                            _cache.Set(IdentityKey(uid), rows.GetValueOrDefault(uid) ?? PatientIdentity.Unknown, TimeSpan.FromMinutes(5));
                        }
                    }
                }
                catch (Exception ex) when (ex is SqlException or InvalidOperationException)
                {
                    _logger.LogError(ex, "Dashboard patient identity lookup failed");
                    warnings.Add("ดึง HN/ชื่อ-สกุลผู้ป่วยไม่สำเร็จ");
                    return pending;
                }
            }

            return pending.Select(p => _cache.TryGetValue(IdentityKey(p.VisitUid), out PatientIdentity? id) && id is not null
                ? p with { Hn = id.Hn, PatientName = id.Name }
                : p).ToList();
        }

        private static string IdentityKey(long visitUid) => $"his:identity:{visitUid}";

        private async Task<Dictionary<long, PatientIdentity>> QueryIdentityAsync(long[] visitUids, CancellationToken cancellationToken)
        {
            // พารามิเตอร์ @u0..@uN เท่านั้น — ไม่ต่อค่าลงในสตริง SQL
            var names = Enumerable.Range(0, visitUids.Length).Select(i => "@u" + i).ToArray();
            var sql = HisQueries.PendingIdentity.Replace("/*VISIT_UIDS*/", string.Join(",", names));

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text, CommandTimeout = _commandTimeoutSeconds };
            for (var i = 0; i < visitUids.Length; i++)
            {
                cmd.Parameters.Add(new SqlParameter(names[i], SqlDbType.BigInt) { Value = visitUids[i] });
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            return ReadIdentities(reader);
        }

        internal static Dictionary<long, PatientIdentity> ReadIdentities(IDataReader r)
        {
            int uid = r.GetOrdinal("VisitUid"), hn = r.GetOrdinal("Hn"), title = r.GetOrdinal("Title"),
                first = r.GetOrdinal("FirstName"), last = r.GetOrdinal("LastName"), anon = r.GetOrdinal("Anon");
            var rows = new Dictionary<long, PatientIdentity>();
            while (r.Read())
            {
                string Str(int i) => r.IsDBNull(i) ? string.Empty : Convert.ToString(r.GetValue(i))?.Trim() ?? string.Empty;
                var key = Convert.ToInt64(r.GetValue(uid));
                if (Str(anon).Equals("Y", StringComparison.OrdinalIgnoreCase))
                {
                    rows[key] = PatientIdentity.Anonymous;
                    continue;
                }

                var name = string.Join(" ", new[] { Str(title), Str(first), Str(last) }.Where(s => s.Length > 0));
                rows[key] = new PatientIdentity(Str(hn).Length > 0 ? Str(hn) : null, name.Length > 0 ? name : null);
            }
            return rows;
        }

        private async Task<T?> Section<T>(string name, List<string> warnings, Func<Task<T>> load) where T : class
        {
            try
            {
                return await load();
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException or IndexOutOfRangeException or InvalidCastException)
            {
                _logger.LogError(ex, "Dashboard section '{Section}' failed", name);
                lock (warnings) warnings.Add($"ดึงข้อมูล \"{name}\" ไม่สำเร็จ");
                return null;
            }
        }

        private async Task<T> Cached<T>(string key, TimeSpan duration, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken)
        {
            if (_cache.TryGetValue(key, out T? value) && value is not null)
            {
                return value;
            }

            value = await factory(cancellationToken);
            _cache.Set(key, value, duration);
            return value;
        }

        private async Task<T> QueryAsync<T>(string sql, Func<SqlDataReader, T> read, CancellationToken cancellationToken, params (string Name, DateTime Value)[] parameters)
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);

            await using var cmd = new SqlCommand(sql, conn) { CommandType = CommandType.Text, CommandTimeout = _commandTimeoutSeconds };
            foreach (var (name, value) in parameters)
            {
                cmd.Parameters.Add(new SqlParameter(name, SqlDbType.DateTime) { Value = value });
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            return read(reader);
        }

        // ==================== Readers ====================

        internal static List<RevenueRow> ReadRevenueRows(IDataReader r)
        {
            int date = r.GetOrdinal("BillDate"), side = r.GetOrdinal("Side"), credit = r.GetOrdinal("IsCredit"),
                code = r.GetOrdinal("PayorCategoryCode"), name = r.GetOrdinal("PayorCategory"), bills = r.GetOrdinal("Bills"), amount = r.GetOrdinal("NetAmount");
            var rows = new List<RevenueRow>();
            while (r.Read())
            {
                rows.Add(new RevenueRow(
                    DateOnly.FromDateTime(r.GetDateTime(date)),
                    r.GetString(side),
                    Convert.ToInt32(r.GetValue(credit)) == 1,
                    r.GetString(code),
                    r.GetString(name),
                    Convert.ToInt32(r.GetValue(bills)),
                    r.IsDBNull(amount) ? 0m : r.GetDecimal(amount)));
            }
            return rows;
        }

        internal static List<PaymentModeAmount> ReadCollections(IDataReader r)
        {
            int mode = r.GetOrdinal("PayMode"), receipts = r.GetOrdinal("Receipts"), received = r.GetOrdinal("Received"),
                refunded = r.GetOrdinal("Refunded"), deposit = r.GetOrdinal("Deposit"), net = r.GetOrdinal("NetCollected");
            var rows = new List<PaymentModeAmount>();
            while (r.Read())
            {
                var code = r.GetString(mode);
                rows.Add(new PaymentModeAmount(code, DashboardText.PaymentModeName(code), Convert.ToInt32(r.GetValue(receipts)),
                    Dec(r, received), Dec(r, refunded), Dec(r, deposit), Dec(r, net)));
            }
            return rows;
        }

        internal static VolumeRow ReadVolume(IDataReader r)
        {
            if (!r.Read()) return new VolumeRow(0, 0, 0);
            return new VolumeRow(
                Convert.ToInt32(r.GetValue(r.GetOrdinal("OpdVisitsToday"))),
                Convert.ToInt32(r.GetValue(r.GetOrdinal("IpdAdmittedNow"))),
                Convert.ToInt32(r.GetValue(r.GetOrdinal("IpdDischargesToday"))));
        }

        internal static List<PendingPayment> ReadPending(IDataReader r, string type, DateTime now)
        {
            int uid = r.GetOrdinal("VisitUid"), since = r.GetOrdinal("WaitingSince"), inBilling = r.GetOrdinal("InBilling"),
                loc = r.GetOrdinal("LocationName"), code = r.GetOrdinal("PayorCategoryCode"), name = r.GetOrdinal("PayorCategory"), amount = r.GetOrdinal("Amount");
            var rows = new List<PendingPayment>();
            while (r.Read())
            {
                var waitingSince = r.IsDBNull(since) ? now : r.GetDateTime(since);
                var visitUid = Convert.ToInt64(r.GetValue(uid));
                rows.Add(new PendingPayment(
                    type,
                    DashboardText.Reference(visitUid),
                    r.GetString(code),
                    r.GetString(name),
                    r.GetString(loc),
                    Dec(r, amount),
                    Math.Max(0, (int)(now - waitingSince).TotalMinutes),
                    Convert.ToInt32(r.GetValue(inBilling)) == 1,
                    visitUid));
            }
            return rows;
        }

        private static decimal Dec(IDataRecord r, int i) => r.IsDBNull(i) ? 0m : Convert.ToDecimal(r.GetValue(i));
    }

    /// <summary>HN และชื่อ-สกุลของผู้ป่วย (PHI) — ใช้เฉพาะ Admin</summary>
    public sealed record PatientIdentity(string? Hn, string? Name)
    {
        public const string ANONYMOUS_NAME = "ผู้ป่วยปกปิดชื่อ";
        public static readonly PatientIdentity Anonymous = new(null, ANONYMOUS_NAME);
        public static readonly PatientIdentity Unknown = new(null, null);
    }

    public sealed record RevenueRow(DateOnly Date, string Side, bool IsCredit, string PayorCategoryCode, string PayorCategory, int Bills, decimal NetAmount);

    public sealed record VolumeRow(int OpdVisitsToday, int IpdAdmittedNow, int IpdDischargesToday);

    /// <summary>
    /// รวมแถวจาก SQL เป็น FinanceDashboard — แยกออกมาเพื่อทดสอบได้โดยไม่ต้องต่อฐานข้อมูล
    /// </summary>
    public static class FinanceDashboardBuilder
    {
        public static FinanceDashboard Build(DateTime now, IReadOnlyList<RevenueRow> revenue, IReadOnlyList<PaymentModeAmount> collections,
            VolumeRow? volume, IReadOnlyList<PendingPayment> pending, IReadOnlyList<string> warnings, bool isSample = false)
        {
            var today = DateOnly.FromDateTime(now);
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            var previousMonthStart = monthStart.AddMonths(-1);
            // วันเดียวกันของเดือนก่อน (ถ้าเดือนก่อนสั้นกว่า ใช้วันสุดท้ายของเดือนนั้น)
            var previousMonthSameDay = previousMonthStart.AddDays(Math.Min(today.Day, DateTime.DaysInMonth(previousMonthStart.Year, previousMonthStart.Month)) - 1);

            static RevenueSplit Split(IEnumerable<RevenueRow> rows) => new()
            {
                Opd = rows.Where(r => r.Side == "O").Sum(r => r.NetAmount),
                Ipd = rows.Where(r => r.Side == "I").Sum(r => r.NetAmount),
            };

            var month = revenue.Where(r => r.Date >= monthStart && r.Date <= today).ToList();
            var previous = revenue.Where(r => r.Date >= previousMonthStart && r.Date <= previousMonthSameDay).ToList();

            var daily = Enumerable.Range(0, 30)
                .Select(i => today.AddDays(i - 29))
                .Select(d =>
                {
                    var rows = revenue.Where(r => r.Date == d).ToList();
                    return new DailyRevenue(d, rows.Where(r => r.Side == "O").Sum(r => r.NetAmount), rows.Where(r => r.Side == "I").Sum(r => r.NetAmount));
                })
                .ToList();

            var byPayer = month
                .GroupBy(r => (r.PayorCategoryCode, r.PayorCategory))
                .Select(g => new PayerRevenue(g.Key.PayorCategoryCode, g.Key.PayorCategory,
                    g.Where(r => r.Side == "O").Sum(r => r.NetAmount), g.Where(r => r.Side == "I").Sum(r => r.NetAmount)))
                .OrderByDescending(p => p.Total)
                .ToList();

            // แสดง 6 กลุ่มใหญ่ที่สุด ที่เหลือรวมเป็น "อื่นๆ" เพื่อให้กราฟอ่านง่าย
            if (byPayer.Count > 7)
            {
                var others = byPayer.Skip(6).ToList();
                byPayer = [.. byPayer.Take(6), new PayerRevenue("OTHER", "อื่นๆ", others.Sum(p => p.Opd), others.Sum(p => p.Ipd))];
            }

            int Bills(string side) => month.Where(r => r.Side == side).Sum(r => r.Bills);
            var monthSplit = Split(month);

            return new FinanceDashboard
            {
                GeneratedAt = now,
                IsSample = isSample,
                DataSource = isSample ? "ข้อมูลตัวอย่าง" : "HIS HealthObject",
                Warnings = warnings,
                RevenueToday = Split(revenue.Where(r => r.Date == today)),
                RevenueMonth = monthSplit,
                RevenuePreviousMonthToDate = Split(previous),
                CashBilledMonth = Split(month.Where(r => !r.IsCredit)),
                CreditBilledMonth = Split(month.Where(r => r.IsCredit)),
                CreditBillsMonth = month.Where(r => r.IsCredit).Sum(r => r.Bills),
                DailyRevenue = daily,
                RevenueByPayer = byPayer,
                CollectionsToday = collections.OrderByDescending(c => c.NetCollected).ToList(),
                Volume = new PatientVolume
                {
                    OpdVisitsToday = volume?.OpdVisitsToday ?? 0,
                    IpdAdmittedNow = volume?.IpdAdmittedNow ?? 0,
                    IpdDischargesToday = volume?.IpdDischargesToday ?? 0,
                    AvgBillOpd = Bills("O") == 0 ? 0 : Math.Round(monthSplit.Opd / Bills("O"), 2),
                    AvgBillIpd = Bills("I") == 0 ? 0 : Math.Round(monthSplit.Ipd / Bills("I"), 2),
                },
                PendingPayments = pending,
            };
        }
    }

    /// <summary>
    /// SQL จากไฟล์ Database/his/queries/*.sql (EmbeddedResource)
    /// </summary>
    internal static class HisQueries
    {
        public static readonly string RevenueDaily = Load("dashboard-revenue-daily.sql");
        public static readonly string Collections = Load("dashboard-collections.sql");
        public static readonly string Volume = Load("dashboard-volume.sql");
        public static readonly string PendingOpd = Load("dashboard-pending-opd.sql");
        public static readonly string PendingIpd = Load("dashboard-pending-ipd.sql");
        public static readonly string NhsoFiscal = Load("report-nhso-fiscal.sql");
        public static readonly string PendingIdentity = Load("dashboard-pending-identity.sql");

        private static string Load(string fileName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}
