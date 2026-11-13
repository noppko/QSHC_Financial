using Financial.Data;
using Financial.Models;
using Financial.Models.Receivables;
using Financial.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Financial.Services.Receivables
{
    /// <param name="Skipped">บิลที่ไม่เข้า 4 สิทธิ์ (หลังจัดสิทธิ์ด้วย SchemeOf)</param>
    /// <param name="CancelSkipped">true = HIS ไม่คืนบิลเลย จึงไม่ยกเลิกลูกหนี้เดิม (กันยกเลิกทั้งหมดเพราะ query ผิดปกติ)</param>
    /// <param name="Duplicates">แถวที่ไม่บันทึกเพราะซ้ำ: บิลเดียวกันซ้ำในผลจาก HIS หรือเลขที่บิลมีอยู่แล้วด้วย UID อื่น</param>
    public sealed record ReceivableSyncResult(int SourceRows, int Added, int Updated, int Cancelled, int Skipped, bool CancelSkipped = false, int Duplicates = 0);

    /// <summary>
    /// ตั้งหนี้จาก HIS: upsert ตาม HisBillUid ในช่วงวันออกบิลที่เลือก
    /// ตรวจซ้ำก่อนบันทึกลง QSHC_Financial:
    ///   1) บิลเดียวกัน (UID) ซ้ำในผลจาก HIS → ใช้แถวแรก
    ///   2) UID มีอยู่แล้วในฐาน แม้วันออกบิลอยู่นอกช่วงที่ดึง → อัปเดตแถวเดิม ไม่สร้างใหม่
    ///   3) เลขที่บิลมีอยู่แล้วแต่คนละ UID → ไม่บันทึก + log ให้ตรวจ
    ///   (ฐานมี unique index ของ HisBillUid กันซ้ำอีกชั้น)
    /// - บิลใหม่ → OPEN · ยอดบิลเปลี่ยน → อัปเดต + ลงประวัติ
    /// - ลูกหนี้ OPEN ที่ยังไม่มีรับชำระ แต่ไม่พบใน HIS แล้ว (ยกเลิก/คืนเงิน/ไม่ใช่บิลเครดิตแล้ว) → CANCELLED + ลงประวัติ
    /// </summary>
    public class ReceivableSyncService
    {
        private readonly ApplicationDbContext _db;
        private readonly IReceivableHisSource _his;
        private readonly FiscalReportOptions _fiscal;
        private readonly ILogger<ReceivableSyncService> _logger;

        private readonly ReceivableOptions _options;

        /// <summary>จำนวนค่าต่อ IN (...) — ไม่ให้เกินขีดจำกัดพารามิเตอร์ของ SQL Server</summary>
        private const int KEY_CHUNK = 500;

        public ReceivableSyncService(ApplicationDbContext db, IReceivableHisSource his, IOptions<FiscalReportOptions> fiscal, ILogger<ReceivableSyncService> logger,
            IOptions<ReceivableOptions>? options = null)
        {
            _db = db;
            _his = his;
            _fiscal = fiscal.Value;
            _logger = logger;
            _options = options?.Value ?? new ReceivableOptions();
        }

        public async Task<ReceivableSyncResult> SyncAsync(DateTime from, DateTime to, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default)
        {
            // query HIS (ช้า) ก่อนแตะฐาน QSHC_Financial
            var bills = await _his.LoadAsync(from, to, rowsRead, cancellationToken);
            var now = DateTime.Now; // เวลาไทย

            var existing = await _db.ArReceivables
                .Where(r => r.BillDate >= from && r.BillDate < to)
                .ToDictionaryAsync(r => r.HisBillUid, cancellationToken);
            var inWindow = existing.Keys.ToHashSet();

            int added = 0, updated = 0, cancelled = 0, skipped = 0, duplicates = 0;

            // จัดสิทธิ์ + ตรวจซ้ำในผลจาก HIS ก่อน (ข้อ 1)
            var seen = new HashSet<long>();
            var accepted = new List<(HisCreditBill Bill, string Scheme)>();
            foreach (var bill in bills)
            {
                var scheme = _fiscal.SchemeOf(bill.PayorCategoryCode, bill.PayorName, bill.AgreementName);
                if (scheme is null) { skipped++; continue; }
                if (!seen.Add(bill.BillUid)) { duplicates++; continue; }
                accepted.Add((bill, scheme));
            }

            // ข้อ 2: UID ที่มีในฐานแล้วแต่วันออกบิลอยู่นอกช่วง → โหลดมาอัปเดต
            var unknownUids = accepted.Select(a => a.Bill.BillUid).Where(uid => !existing.ContainsKey(uid)).ToList();
            foreach (var chunk in unknownUids.Chunk(KEY_CHUNK))
            {
                foreach (var r in await _db.ArReceivables.Where(r => chunk.Contains(r.HisBillUid)).ToListAsync(cancellationToken))
                    existing.TryAdd(r.HisBillUid, r);
            }

            // ข้อ 3: เลขที่บิลของบิลใหม่ ที่มีอยู่แล้วด้วย UID อื่น
            var newBillNumbers = accepted.Where(a => !existing.ContainsKey(a.Bill.BillUid) && !string.IsNullOrEmpty(a.Bill.BillNumber))
                .Select(a => a.Bill.BillNumber).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var takenBillNumbers = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            foreach (var chunk in newBillNumbers.Chunk(KEY_CHUNK))
            {
                foreach (var x in await _db.ArReceivables.AsNoTracking().Where(r => chunk.Contains(r.BillNumber)).Select(r => new { r.BillNumber, r.HisBillUid }).ToListAsync(cancellationToken))
                    takenBillNumbers.TryAdd(x.BillNumber, x.HisBillUid);
            }
            var newInThisRun = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var zoneCache = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var (bill, scheme) in accepted)
            {
                if (!existing.ContainsKey(bill.BillUid) && !string.IsNullOrEmpty(bill.BillNumber)
                    && ((takenBillNumbers.TryGetValue(bill.BillNumber, out var otherUid) && otherUid != bill.BillUid) || !newInThisRun.Add(bill.BillNumber)))
                {
                    duplicates++;
                    _logger.LogWarning("Receivable sync: bill number already stored with another HIS bill UID — skipped (HIS bill UID {Uid})", bill.BillUid);
                    continue;
                }

                var agreement = bill.AgreementName ?? string.Empty;
                if (!zoneCache.TryGetValue(agreement, out var outOfZone)) zoneCache[agreement] = outOfZone = _fiscal.IsOutOfZone(agreement);

                if (existing.TryGetValue(bill.BillUid, out var r))
                {
                    var changed = false;
                    if (r.BilledAmount != bill.NetAmount)
                    {
                        var note = $"ยอดบิลใน HIS เปลี่ยน {r.BilledAmount:N2} → {bill.NetAmount:N2}";
                        AddHistory(r, r.StatusCode, r.StatusCode, note, now);
                        // เช่น เภสัชคีย์ยาเพิ่มหลังจำหน่าย — ส่งเรียกเก็บ/ได้ผลแล้ว ต้องให้คนตรวจ (อาจต้องเรียกเก็บส่วนต่างเพิ่ม)
                        if (r.IsBeyondOpen) Flag(r, $"{note} หลังส่งเรียกเก็บ/มีผลการเรียกเก็บแล้ว", now);
                        changed = true;
                    }
                    if (r.ReviewReason?.StartsWith(MISSING_IN_HIS, StringComparison.Ordinal) == true)
                    {
                        AddHistory(r, r.StatusCode, r.StatusCode, "พบบิลใน HIS อีกครั้ง — ล้างธงต้องตรวจ", now);
                        r.ReviewReason = null;
                        r.ReviewFlaggedAt = null;
                        changed = true;
                    }
                    if (r.StatusCode == ArStatusCodes.CANCELLED)
                    {
                        AddHistory(r, r.StatusCode, ArStatusCodes.OPEN, "พบบิลเครดิตใน HIS อีกครั้ง", now);
                        r.StatusCode = ArStatusCodes.OPEN;
                        changed = true;
                    }
                    changed |= Apply(r, bill, scheme, outOfZone);
                    if (r.PaidAmount > 0 || r.DeniedAmount > 0)
                    {
                        // มีผลการเรียกเก็บแล้ว ยอดบิลเปลี่ยน → สถานะตามยอดใหม่ (เช่น PAID → PARTIAL)
                        var derived = r.DeriveStatus(_options.PaidTolerance, hasResults: true);
                        if (derived != r.StatusCode)
                        {
                            AddHistory(r, r.StatusCode, derived, "สถานะเปลี่ยนตามยอดบิลใน HIS", now);
                            r.StatusCode = derived;
                            changed = true;
                        }
                    }
                    r.LastSyncedAt = now;
                    if (changed) updated++;
                }
                else
                {
                    r = new ArReceivable { HisBillUid = bill.BillUid, StatusCode = ArStatusCodes.OPEN, LastSyncedAt = now };
                    Apply(r, bill, scheme, outOfZone);
                    _db.ArReceivables.Add(r);
                    added++;
                }
            }

            var cancelSkipped = false;
            // ยกเลิกเฉพาะลูกหนี้ที่วันออกบิลอยู่ในช่วงที่ดึง (แถวนอกช่วงที่โหลดมาตรวจซ้ำไม่เกี่ยว)
            var gone = existing.Values.Where(r => inWindow.Contains(r.HisBillUid) && !seen.Contains(r.HisBillUid)
                && r.StatusCode is not (ArStatusCodes.CANCELLED or ArStatusCodes.WRITE_OFF)).ToList();
            var missing = gone.Where(r => !r.IsBeyondOpen).ToList();
            // ส่งเรียกเก็บ/มีผลแล้ว แต่บิลหายจาก HIS (เช่น ยกเลิกบิลแล้วออกบิลใหม่เพราะคีย์ยาเพิ่ม) → ไม่ยกเลิกเอง ติดธงให้ตรวจ
            var flagged = 0;
            if (bills.Count > 0)
            {
                foreach (var r in gone.Where(r => r.IsBeyondOpen && r.ReviewReason?.StartsWith(MISSING_IN_HIS, StringComparison.Ordinal) != true))
                {
                    Flag(r, $"{MISSING_IN_HIS} (ยกเลิก/แก้บิลหลังส่งเรียกเก็บ) — ตรวจบิลใหม่ของ visit เดียวกัน", now);
                    AddHistory(r, r.StatusCode, r.StatusCode, r.ReviewReason!, now);
                    flagged++;
                }
            }
            if (bills.Count == 0 && missing.Count > 0)
            {
                // HIS คืน 0 แถวทั้งที่เคยมีลูกหนี้ในช่วงนี้ — น่าจะผิดปกติ ไม่ยกเลิกทั้งหมด
                cancelSkipped = true;
                _logger.LogWarning("Receivable sync {From:yyyy-MM-dd}–{To:yyyy-MM-dd}: HIS returned no bills; {Count} open receivables kept", from, to, missing.Count);
            }
            else
            {
                foreach (var r in missing)
                {
                    AddHistory(r, r.StatusCode, ArStatusCodes.CANCELLED, "ไม่พบบิลเครดิตใน HIS แล้ว (ยกเลิก/คืนเงิน/เปลี่ยนเป็นเงินสด/ไม่อยู่ใน 4 สิทธิ์)", now);
                    r.StatusCode = ArStatusCodes.CANCELLED;
                    r.LastSyncedAt = now;
                    cancelled++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();

            _logger.LogInformation("Receivable sync {From:yyyy-MM-dd}–{To:yyyy-MM-dd}: {Rows} HIS bills, {Added} added, {Updated} updated, {Cancelled} cancelled, {Skipped} not in schemes, {Duplicates} duplicates skipped, {Flagged} flagged for review",
                from, to, bills.Count, added, updated, cancelled, skipped, duplicates, flagged);
            return new ReceivableSyncResult(bills.Count, added, updated, cancelled, skipped, cancelSkipped, duplicates);
        }

        public const string MISSING_IN_HIS = "ไม่พบบิลเครดิตใน HIS แล้ว";

        private static void Flag(ArReceivable r, string reason, DateTime at)
        {
            r.ReviewReason = reason.Length <= 300 ? reason : reason[..300];
            r.ReviewFlaggedAt = at;
        }

        /// <returns>true ถ้าค่าที่มีผลต่อการแสดงผลเปลี่ยน</returns>
        private bool Apply(ArReceivable r, HisCreditBill bill, string scheme, bool outOfZone)
        {
            var type = _fiscal.ResolvePatientType(bill.BillSide, bill.VisitEncounterCode).Type;
            var before = (r.BilledAmount, r.SchemeCode, r.PatientTypeCode, r.PayorCode, r.AgreementName, r.VisitNumber, r.DischargeDate);

            r.BillNumber = Truncate(bill.BillNumber, 30) ?? string.Empty;
            r.BillDate = bill.BillDate;
            r.HisVisitUid = bill.VisitUid;
            r.VisitNumber = Truncate(bill.VisitNumber, 20);
            r.Hn = Truncate(bill.Hn, 20);
            r.PatientTypeCode = type;
            r.VisitStartDate = bill.VisitStart;
            r.DischargeDate = bill.VisitEnd;
            r.SchemeCode = scheme;
            r.PayorCode = Truncate(bill.PayorCode, 50);
            r.PayorName = Truncate(bill.PayorName, 200);
            r.PayorCategoryCode = Truncate(bill.PayorCategoryCode, 10);
            r.AgreementName = Truncate(bill.AgreementName, 400);
            r.IsOutOfZone = outOfZone;
            r.BilledAmount = bill.NetAmount;
            r.Recalculate();

            return before != (r.BilledAmount, r.SchemeCode, r.PatientTypeCode, r.PayorCode, r.AgreementName, r.VisitNumber, r.DischargeDate);
        }

        private static void AddHistory(ArReceivable r, string? from, string to, string reason, DateTime at)
            => r.StatusHistories.Add(new ArStatusHistory { FromStatus = from, ToStatus = to, Reason = reason, ChangedAt = at });

        private static string? Truncate(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
    }
}
