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
    public sealed record ReceivableSyncResult(int SourceRows, int Added, int Updated, int Cancelled, int Skipped, bool CancelSkipped = false);

    /// <summary>
    /// ตั้งหนี้จาก HIS: upsert ตาม HisBillUid ในช่วงวันออกบิลที่เลือก
    /// - บิลใหม่ → OPEN · ยอดบิลเปลี่ยน → อัปเดต + ลงประวัติ
    /// - ลูกหนี้ OPEN ที่ยังไม่มีรับชำระ แต่ไม่พบใน HIS แล้ว (ยกเลิก/คืนเงิน/ไม่ใช่บิลเครดิตแล้ว) → CANCELLED + ลงประวัติ
    /// </summary>
    public class ReceivableSyncService
    {
        private readonly ApplicationDbContext _db;
        private readonly IReceivableHisSource _his;
        private readonly FiscalReportOptions _fiscal;
        private readonly ILogger<ReceivableSyncService> _logger;

        public ReceivableSyncService(ApplicationDbContext db, IReceivableHisSource his, IOptions<FiscalReportOptions> fiscal, ILogger<ReceivableSyncService> logger)
        {
            _db = db;
            _his = his;
            _fiscal = fiscal.Value;
            _logger = logger;
        }

        public async Task<ReceivableSyncResult> SyncAsync(DateTime from, DateTime to, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default)
        {
            // query HIS (ช้า) ก่อนแตะฐาน QSHC_Financial
            var bills = await _his.LoadAsync(from, to, rowsRead, cancellationToken);
            var now = DateTime.Now; // เวลาไทย

            var existing = await _db.ArReceivables
                .Where(r => r.BillDate >= from && r.BillDate < to)
                .ToDictionaryAsync(r => r.HisBillUid, cancellationToken);

            int added = 0, updated = 0, cancelled = 0, skipped = 0;
            var seen = new HashSet<long>();
            var zoneCache = new Dictionary<string, bool>(StringComparer.Ordinal);

            foreach (var bill in bills)
            {
                var scheme = _fiscal.SchemeOf(bill.PayorCategoryCode, bill.PayorName, bill.AgreementName);
                if (scheme is null) { skipped++; continue; }
                if (!seen.Add(bill.BillUid)) continue;

                var agreement = bill.AgreementName ?? string.Empty;
                if (!zoneCache.TryGetValue(agreement, out var outOfZone)) zoneCache[agreement] = outOfZone = _fiscal.IsOutOfZone(agreement);

                if (existing.TryGetValue(bill.BillUid, out var r))
                {
                    var changed = false;
                    if (r.BilledAmount != bill.NetAmount)
                    {
                        AddHistory(r, r.StatusCode, r.StatusCode, $"ยอดบิลใน HIS เปลี่ยน {r.BilledAmount:N2} → {bill.NetAmount:N2}", now);
                        changed = true;
                    }
                    if (r.StatusCode == ArStatusCodes.CANCELLED)
                    {
                        AddHistory(r, r.StatusCode, ArStatusCodes.OPEN, "พบบิลเครดิตใน HIS อีกครั้ง", now);
                        r.StatusCode = ArStatusCodes.OPEN;
                        changed = true;
                    }
                    changed |= Apply(r, bill, scheme, outOfZone);
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
            var missing = existing.Values.Where(r => !seen.Contains(r.HisBillUid) && r.StatusCode == ArStatusCodes.OPEN && r.PaidAmount == 0).ToList();
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

            _logger.LogInformation("Receivable sync {From:yyyy-MM-dd}–{To:yyyy-MM-dd}: {Rows} HIS bills, {Added} added, {Updated} updated, {Cancelled} cancelled, {Skipped} not in schemes",
                from, to, bills.Count, added, updated, cancelled, skipped);
            return new ReceivableSyncResult(bills.Count, added, updated, cancelled, skipped, cancelSkipped);
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
