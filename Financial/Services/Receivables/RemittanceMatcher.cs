using Financial.Models;

namespace Financial.Services.Receivables
{
    /// <summary>ลูกหนี้ที่ใช้จับคู่ (เฉพาะคอลัมน์ที่จำเป็น)</summary>
    public sealed record MatchCandidate(int Id, string SchemeCode, string? VisitNumber, string? Hn, DateTime? VisitStartDate, DateTime? DischargeDate,
        string BillNumber, decimal BilledAmount, decimal OutstandingAmount, string StatusCode);

    public sealed record MatchOutcome(string Result, string? Step, int? ReceivableId, IReadOnlyList<int> CandidateIds, string? Message);

    public sealed record MatchSettings(decimal Tolerance = 1m, int SuggestDays = 1, decimal SuggestAmountPercent = 1m)
    {
        public static MatchSettings From(ReceivableOptions o) => new(o.PaidTolerance, Math.Max(0, o.SuggestDays), Math.Max(0, o.SuggestAmountPercent));
    }

    /// <summary>
    /// จับคู่ 1 แถวผลการเรียกเก็บกับลูกหนี้ของสิทธิ์เดียวกัน (ไม่จับคู่ข้ามสิทธิ์ / ไม่จับคู่ลูกหนี้ที่ยกเลิก)
    /// 1) VN/AN (+ เลขบิล) → 2) HN + วันที่เปิด visit/จำหน่าย → 3) HN + วันที่ ± N วัน + ยอดใกล้เคียง (SUGGESTED)
    /// </summary>
    public static class RemittanceMatcher
    {
        public const string STEP_VN = "1";
        public const string STEP_HN_DATE = "2";
        public const string STEP_SUGGEST = "3";
        public const string STEP_MANUAL = "MANUAL";

        public static MatchOutcome Match(RemittanceRow row, string schemeCode, IReadOnlyCollection<MatchCandidate> candidates, MatchSettings settings)
        {
            if (row.Error is not null) return new(ArMatchResultCodes.INVALID, null, null, [], row.Error);

            var inScheme = candidates.Where(c => c.SchemeCode == schemeCode && c.StatusCode != ArStatusCodes.CANCELLED).ToList();

            // ขั้น 1: VN/AN
            if (!string.IsNullOrEmpty(row.VisitNumber))
            {
                var byVn = Narrow(inScheme.Where(c => Same(c.VisitNumber, row.VisitNumber)).ToList(), row.BillNumber);
                if (byVn.Count == 1) return Single(row, byVn[0], STEP_VN, settings);
                if (byVn.Count > 1) return Multiple(byVn, STEP_VN, $"VN/AN นี้มี {byVn.Count} บิลของสิทธิ์นี้ — ยืนยันเพื่อกระจายยอดตามสัดส่วนคงค้าง หรือผูกเลขบิลเอง");
            }

            // ขั้น 2 และ 3: HN + วันที่
            if (!string.IsNullOrEmpty(row.Hn) && row.VisitDate is DateTime date)
            {
                var byHn = inScheme.Where(c => Same(c.Hn, row.Hn)).ToList();
                var exact = Narrow(byHn.Where(c => c.VisitStartDate?.Date == date || c.DischargeDate?.Date == date).ToList(), row.BillNumber);
                if (exact.Count == 1) return Single(row, exact[0], STEP_HN_DATE, settings);
                if (exact.Count > 1) return Multiple(exact, STEP_HN_DATE, $"HN + วันที่นี้มี {exact.Count} บิล — ยืนยันเพื่อกระจายยอด หรือผูกเลขบิลเอง");

                var target = row.ClaimedAmount ?? row.ReceivedAmount + row.DeniedAmount;
                if (target > 0)
                {
                    var near = byHn.Where(c => c.VisitStartDate is DateTime start
                            && Math.Abs((start.Date - date).TotalDays) <= settings.SuggestDays
                            && Math.Abs(c.BilledAmount - target) <= Math.Max(settings.Tolerance, c.BilledAmount * settings.SuggestAmountPercent / 100m))
                        .ToList();
                    if (near.Count == 1)
                    {
                        var single = Single(row, near[0], STEP_SUGGEST, settings);
                        return single with
                        {
                            Result = ArMatchResultCodes.SUGGESTED,
                            Message = $"วันที่ใกล้เคียง (± {settings.SuggestDays} วัน) และยอดใกล้เคียง — ตรวจแล้วติ๊กยอมรับ" + (single.Message is null ? "" : " · " + single.Message),
                        };
                    }
                }
            }

            // ไม่พบในสิทธิ์นี้ — บอกถ้าเป็นลูกหนี้ของสิทธิ์อื่น
            var other = candidates.FirstOrDefault(c => c.SchemeCode != schemeCode && c.StatusCode != ArStatusCodes.CANCELLED
                && (Same(c.VisitNumber, row.VisitNumber) || (Same(c.Hn, row.Hn) && row.VisitDate is not null && c.VisitStartDate?.Date == row.VisitDate)));
            return new(ArMatchResultCodes.NOT_FOUND, null, null, [], other is null
                ? "ไม่พบลูกหนี้ของสิทธิ์นี้ (อาจยังไม่ได้ดึงจาก HIS หรือ VN/AN/HN/วันที่ไม่ตรง) — ผูกเลขบิลเองได้"
                : $"เป็นลูกหนี้สิทธิ์ {other.SchemeCode} — ไม่จับคู่ข้ามสิทธิ์");
        }

        /// <summary>ผลเมื่อได้ลูกหนี้ 1 รายการ: ปฏิเสธ / ยอดตรง / ยอดต่าง</summary>
        public static MatchOutcome Single(RemittanceRow row, MatchCandidate c, string step, MatchSettings settings)
        {
            if (row.ReceivedAmount == 0 && (row.DeniedAmount > 0 || !string.IsNullOrEmpty(row.ReasonCode) || !string.IsNullOrEmpty(row.ReasonText)))
            {
                // เหตุผลแสดงจาก ReasonCode/ReasonText ของแถวอยู่แล้ว ไม่ต้องซ้ำใน Message
                return new(ArMatchResultCodes.DENIED, step, c.Id, [], null);
            }
            var diff = row.ReceivedAmount - c.OutstandingAmount;
            if (Math.Abs(diff) <= settings.Tolerance) return new(ArMatchResultCodes.MATCHED, step, c.Id, [], null);
            return new(ArMatchResultCodes.AMOUNT_DIFF, step, c.Id, [],
                $"ได้ {row.ReceivedAmount:N2} จากคงค้าง {c.OutstandingAmount:N2} ({(diff > 0 ? "เกิน" : "ขาด")} {Math.Abs(diff):N2})");
        }

        private static MatchOutcome Multiple(IReadOnlyList<MatchCandidate> list, string step, string message)
            => new(ArMatchResultCodes.MULTIPLE, step, null, list.Select(c => c.Id).Order().ToList(), message);

        /// <summary>มีเลขบิลในไฟล์และตรงกับบางรายการ → ใช้เฉพาะรายการนั้น</summary>
        private static List<MatchCandidate> Narrow(List<MatchCandidate> list, string? billNumber)
        {
            if (string.IsNullOrEmpty(billNumber) || list.Count <= 1) return list;
            var byBill = list.Where(c => Same(c.BillNumber, billNumber)).ToList();
            return byBill.Count > 0 ? byBill : list;
        }

        private static bool Same(string? a, string? b)
            => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b) && string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        /// <summary>กระจายยอดตามสัดส่วน (ปัดทศนิยม 2 ตำแหน่ง เศษไปรายการสุดท้าย) — น้ำหนักรวม 0 แบ่งเท่ากัน</summary>
        public static IReadOnlyList<decimal> Allocate(decimal amount, IReadOnlyList<decimal> weights)
        {
            if (weights.Count == 0) return [];
            var positive = weights.Select(w => Math.Max(0, w)).ToList();
            var total = positive.Sum();
            var result = new List<decimal>();
            for (var i = 0; i < positive.Count - 1; i++)
            {
                result.Add(Math.Round(total == 0 ? amount / positive.Count : amount * positive[i] / total, 2, MidpointRounding.AwayFromZero));
            }
            result.Add(amount - result.Sum());
            return result;
        }
    }
}
