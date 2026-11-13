using Financial.Services.Dashboard;
using Financial.Services.Reports;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using System.Data;

namespace Financial.Services.Receivables
{
    /// <summary>การตั้งค่าลูกหนี้สิทธิ (appsettings.json → Receivables)</summary>
    public class ReceivableOptions
    {
        public const string SECTION_NAME = "Receivables";

        /// <summary>
        /// วันเริ่มตั้งหนี้ (ตามวันออกบิล) — ไม่ดึงบิลก่อนวันนี้ · ไม่ระบุ = วันแรกของปีงบปัจจุบัน (1 ต.ค.)
        /// ฝ่ายการเงินกำหนดเริ่มปีงบ 2570 = 1 ต.ค. 2569 (2026-10-01)
        /// </summary>
        public DateTime? StartDate { get; set; }

        /// <summary>รอบอัตโนมัติทุกคืน ดึงย้อนหลังกี่วัน (ไม่ก่อน StartDate) — บิลเก่ากว่านี้ไม่ค่อยเปลี่ยน</summary>
        public int SyncDaysBack { get; set; } = 90;

        /// <summary>ยังไม่มีลูกหนี้เลยตอนแอปเริ่ม → ดึงตั้งแต่ StartDate ทันที (ไม่ต้องรอรอบกลางคืน)</summary>
        public bool InitialSyncOnStartup { get; set; } = true;

        public DateTime EffectiveStartDate(DateTime now) => StartDate?.Date ?? Reports.FiscalYear.Of(now).Start;

        public bool AutoSyncEnabled { get; set; } = true;

        /// <summary>ชั่วโมงที่ดึงอัตโนมัติ (เวลาไทย) — คนละเวลากับรายงานปีงบ (02:00)</summary>
        public int AutoSyncHour { get; set; } = 3;

        public int CommandTimeoutSeconds { get; set; } = 600;

        /// <summary>ผู้จ่ายที่ไม่ตั้งเป็นลูกหนี้ เช่น PDQSHC001 ส่วนเกินสิทธิ์ (เหมือน AR interface เดิม)</summary>
        public string[] ExcludedPayorCodes { get; set; } = [];

        public static readonly string[] DEFAULT_EXCLUDED_PAYORS = ["PDQSHC001"];

        /// <summary>เกณฑ์ปัดเศษ (บาท): คงค้าง ≤ ค่านี้ถือว่าได้รับครบ และยอดต่าง ≤ ค่านี้ถือว่าตรง</summary>
        public decimal PaidTolerance { get; set; } = 1.00m;

        /// <summary>จับคู่ขั้น 3: วันที่เปิด visit ต่างกันได้ไม่เกิน ± วัน</summary>
        public int SuggestDays { get; set; } = 1;

        /// <summary>จับคู่ขั้น 3: ยอดต่างจากยอดเรียกเก็บได้ไม่เกิน % </summary>
        public decimal SuggestAmountPercent { get; set; } = 1m;

        public int MaxImportRows { get; set; } = 20_000;

        public int MaxImportFileMb { get; set; } = 10;

        public IReadOnlyList<string> EffectiveExcludedPayors
            => (ExcludedPayorCodes.Length > 0 ? ExcludedPayorCodes : DEFAULT_EXCLUDED_PAYORS)
                .Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>บิลเครดิต 1 ใบจาก HIS (ใช้ในหน่วยความจำ — HisVisitUid ห้ามส่งไปหน้าเว็บ)</summary>
    public sealed record HisCreditBill(long BillUid, string BillNumber, DateTime BillDate, string BillSide, string? VisitEncounterCode,
        long VisitUid, string? VisitNumber, string? Hn, DateTime? VisitStart, DateTime? VisitEnd,
        string? PayorCode, string? PayorName, string? PayorCategoryCode, string? AgreementName, decimal NetAmount);

    public interface IReceivableHisSource
    {
        Task<IReadOnlyList<HisCreditBill>> LoadAsync(DateTime from, DateTime to, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default);
    }

    /// <summary>อ่านบิลเครดิตจาก HIS HealthObject (Database/his/queries/ar-credit-bills.sql — SELECT + NOLOCK)</summary>
    public class ReceivableHisSource : IReceivableHisSource
    {
        public const string EXCLUDED_PAYORS_PLACEHOLDER = "/*EXCLUDED_PAYORS*/";
        private const int PROGRESS_EVERY_ROWS = 2_000;

        private readonly string? _connectionString;
        private readonly FiscalReportOptions _fiscal;
        private readonly ReceivableOptions _options;

        public ReceivableHisSource(IConfiguration configuration, IOptions<FiscalReportOptions> fiscal, IOptions<ReceivableOptions> options)
        {
            _connectionString = configuration.GetConnectionString("HealthObject");
            _fiscal = fiscal.Value;
            _options = options.Value;
        }

        public async Task<IReadOnlyList<HisCreditBill>> LoadAsync(DateTime from, DateTime to, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_connectionString)) throw new InvalidOperationException("ยังไม่ได้ตั้งค่า ConnectionStrings:HealthObject");

            var categories = _fiscal.AllPayorCategoryCodes;
            var keywords = _fiscal.AllPayorNameKeywords;
            var excluded = _options.EffectiveExcludedPayors;
            if (categories.Count == 0) throw new InvalidOperationException("ยังไม่ได้ตั้งค่ากลุ่มผู้จ่ายของสิทธิ์ (Reports:Fiscal:Schemes)");

            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            await using var cmd = new SqlCommand(BuildSql(categories.Count, keywords.Count, excluded.Count), conn) { CommandTimeout = _options.CommandTimeoutSeconds };
            cmd.Parameters.Add(new SqlParameter("@from", SqlDbType.DateTime) { Value = from });
            cmd.Parameters.Add(new SqlParameter("@to", SqlDbType.DateTime) { Value = to });
            FiscalHisSource.AddPayorParameters(cmd, categories, keywords);
            for (var i = 0; i < excluded.Count; i++)
            {
                cmd.Parameters.Add(new SqlParameter($"@x{i}", SqlDbType.VarChar, 50) { Value = excluded[i] });
            }

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            return ReadRows(reader, rowsRead, cancellationToken);
        }

        internal static string BuildSql(int categoryCount, int keywordCount, int excludedCount)
            => FiscalHisSource.ApplyPayorFilter(HisQueries.ArCreditBills, categoryCount, keywordCount)
                .Replace(EXCLUDED_PAYORS_PLACEHOLDER, excludedCount == 0
                    ? string.Empty
                    : $"AND ISNULL(PD.Code, '') NOT IN ({string.Join(", ", Enumerable.Range(0, excludedCount).Select(i => $"@x{i}"))})");

        internal static IReadOnlyList<HisCreditBill> ReadRows(IDataReader r, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default)
        {
            int billUid = r.GetOrdinal("BillUid"), billNo = r.GetOrdinal("BillNumber"), billDate = r.GetOrdinal("BillDate"), side = r.GetOrdinal("BillSide"),
                enc = r.GetOrdinal("VisitEncounterCode"), visitUid = r.GetOrdinal("VisitUid"), vn = r.GetOrdinal("VisitNumber"), hn = r.GetOrdinal("Hn"),
                start = r.GetOrdinal("VisitStart"), end = r.GetOrdinal("VisitEnd"), payorCode = r.GetOrdinal("PayorCode"), payorName = r.GetOrdinal("PayorName"),
                cat = r.GetOrdinal("PayorCategoryCode"), agreement = r.GetOrdinal("AgreementName"), amount = r.GetOrdinal("NetAmount");

            string? Text(int i) => r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i))?.Trim();
            DateTime? Date(int i) => r.IsDBNull(i) ? null : Convert.ToDateTime(r.GetValue(i));

            var rows = new List<HisCreditBill>();
            while (r.Read())
            {
                rows.Add(new HisCreditBill(
                    Convert.ToInt64(r.GetValue(billUid)),
                    Text(billNo) ?? string.Empty,
                    Convert.ToDateTime(r.GetValue(billDate)),
                    Text(side) ?? "O",
                    Text(enc),
                    r.IsDBNull(visitUid) ? 0 : Convert.ToInt64(r.GetValue(visitUid)),
                    Text(vn),
                    Text(hn),
                    Date(start),
                    Date(end),
                    Text(payorCode),
                    Text(payorName),
                    Text(cat),
                    Text(agreement),
                    r.IsDBNull(amount) ? 0m : Convert.ToDecimal(r.GetValue(amount))));

                if (rows.Count % PROGRESS_EVERY_ROWS == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    rowsRead?.Report(rows.Count);
                }
            }
            rowsRead?.Report(rows.Count);
            return rows;
        }
    }
}
