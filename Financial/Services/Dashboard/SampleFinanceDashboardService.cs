using Financial.Models.Dashboard;

namespace Financial.Services.Dashboard
{
    /// <summary>
    /// ข้อมูลตัวอย่างสำหรับ Dashboard (ใช้เมื่อ Dashboard:Source = "Sample" เช่น ทดสอบหน้าจอนอกเครือข่ายโรงพยาบาล)
    /// สร้างแถวรูปแบบเดียวกับผล SQL แล้วรวมด้วย FinanceDashboardBuilder ตัวเดียวกับข้อมูลจริง
    /// ค่าเปลี่ยนตามวัน แต่คงที่ภายในวันเดียวกัน (seed จากวันที่) เพื่อให้หน้าจอไม่กระโดดไปมา
    /// </summary>
    public class SampleFinanceDashboardService : IFinanceDashboardService
    {
        // กลุ่มผู้จ่าย PYRACAT (finance.md §5) และสัดส่วนรายได้ ก.ย. 2569
        public static readonly (string Code, string Name, double Share)[] Payers =
        [
            ("B", "กรมบัญชีกลาง", 0.52),
            ("A", "สปสช", 0.30),
            ("F", "เงินสด", 0.10),
            ("C", "ประกันสังคม", 0.04),
            ("G", "บริษัทประกัน", 0.02),
            ("P", "อปท.", 0.02),
        ];

        private static readonly string[] Clinics = ["อายุรกรรมหัวใจ", "ศัลยกรรมหัวใจ", "หัวใจเด็ก", "คลินิกพิเศษนอกเวลา", "ห้องฉุกเฉิน"];
        private static readonly string[] Wards = ["หอผู้ป่วย 5A", "หอผู้ป่วย 5B", "หอผู้ป่วย 6A", "ICU", "CCU", "หอผู้ป่วยพิเศษ 7"];

        private readonly TimeProvider _timeProvider;

        public SampleFinanceDashboardService(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public Task<FinanceDashboard> GetAsync(bool includePatientIdentity = false, CancellationToken cancellationToken = default)
        {
            var now = ThaiClock.Now(_timeProvider);
            var today = DateOnly.FromDateTime(now);
            var rng = new Random(today.DayNumber);
            var dayProgress = Math.Clamp(now.TimeOfDay.TotalHours / 16.0, 0.05, 1.0);

            // รายได้ ~132 ล้านบาท/เดือน ตามสถิติ ก.ย. 2569
            var revenue = new List<RevenueRow>();
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            for (var d = monthStart.AddMonths(-1) < today.AddDays(-29) ? monthStart.AddMonths(-1) : today.AddDays(-29); d <= today; d = d.AddDays(1))
            {
                var weekend = d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0.35 : 1.0;
                var factor = weekend * (d == today ? dayProgress : 1.0);
                foreach (var side in new[] { "O", "I" })
                {
                    var dayTotal = (side == "O" ? 2_100_000 : 3_000_000) * factor * (0.85 + rng.NextDouble() * 0.3);
                    foreach (var p in Payers)
                    {
                        var amount = (decimal)Math.Round(dayTotal * p.Share, 2);
                        var bills = Math.Max(1, (int)(amount / (side == "O" ? 2_400m : 85_000m)));
                        revenue.Add(new RevenueRow(d, side, p.Code != "F", p.Code, p.Name, bills, amount));
                    }
                }
            }

            var collections = new List<PaymentModeAmount>
            {
                Collect("CASHH", 1_900_000), Collect("QRCODE", 1_150_000), Collect("CARDD", 600_000), Collect("TRNFR", 10_000),
            };

            PaymentModeAmount Collect(string code, double perDay)
            {
                var received = (decimal)Math.Round(perDay * dayProgress * (0.8 + rng.NextDouble() * 0.4), 2);
                var refunded = Math.Round(received * 0.01m, 2);
                return new PaymentModeAmount(code, DashboardText.PaymentModeName(code), (int)(received / 3_000m), received, refunded, 0, received - refunded);
            }

            var pending = new List<PendingPayment>();
            for (int i = 0, n = rng.Next(18, 34); i < n; i++)
            {
                var payer = PickPayer(rng);
                var wait = rng.Next(1, 95);
                var premium = rng.NextDouble() < 0.2; // ราว 1 ใน 5 มาจาก Premium Clinic (ห้องการเงินแยก)
                var inBilling = wait > 10;
                // ราว 2 ใน 3 มียา: บางคนยังรอเภสัชคัดกรอง (ยังไม่นับเวลารอการเงิน) ที่เหลือนับจากเวลาคัดกรอง
                string? drug = null;
                int? screening = null;
                if (rng.NextDouble() < 0.65)
                {
                    var screened = inBilling ? rng.NextDouble() < 0.9 : rng.NextDouble() < 0.5; // ข้อมูลตัวอย่าง: มีคนรอคัดกรองให้เห็นเสมอ
                    drug = screened ? (inBilling ? DrugStages.IN_BILLING : DrugStages.SCREENED) : DrugStages.WAIT_SCREENING;
                    if (!screened) { screening = wait; wait = 0; inBilling = false; } // ยังไม่คัดกรอง = การเงินยังรับเรื่องไม่ได้
                    else wait = Math.Max(0, wait - rng.Next(0, Math.Min(wait, 15) + 1));
                }
                pending.Add(Identify(new PendingPayment("O", DashboardText.Reference(rng.Next(1_000_000, 9_999_999)), payer.Code, payer.Name,
                    premium ? "Premium Clinic" : Clinics[rng.Next(Clinics.Length)], Money(rng, 300, payer.Code == "F" ? 25_000 : 8_000), wait, inBilling,
                    Zone: premium ? PendingZones.PREMIUM : PendingZones.OPD, DrugStage: drug, ScreeningMinutes: screening), includePatientIdentity, pending.Count));
            }
            for (int i = 0, n = rng.Next(6, 16); i < n; i++)
            {
                var payer = PickPayer(rng);
                pending.Add(Identify(new PendingPayment("I", DashboardText.Reference(rng.Next(1_000_000, 9_999_999)), payer.Code, payer.Name,
                    Wards[rng.Next(Wards.Length)], Money(rng, 15_000, 650_000), rng.Next(20, 60 * 30), rng.NextDouble() > 0.3, Zone: PendingZones.IPD), includePatientIdentity, pending.Count));
            }

            var volume = new VolumeRow((int)(rng.Next(700, 1000) * dayProgress), rng.Next(80, 110), (int)(rng.Next(10, 25) * dayProgress));

            return Task.FromResult(FinanceDashboardBuilder.Build(now, revenue, collections, volume, pending, [], isSample: true));
        }

        /// <summary>HN/ชื่อสมมติ (ไม่ใช่ข้อมูลจริง) สำหรับทดสอบหน้าจอของ Admin — ขึ้นต้น "ตัวอย่าง" ชัดเจน</summary>
        private static string[] SampleNames => ["สมชาย ใจดี", "สมหญิง รักสุข", "วิชัย มั่นคง", "สุดา แสงทอง", "ประเสริฐ ทดสอบ", "มาลี ตัวอย่าง"];

        private static PendingPayment Identify(PendingPayment p, bool include, int index)
            => include ? p with { Hn = $"69{900000 + index * 37:D6}", PatientName = "(ตัวอย่าง) " + SampleNames[index % SampleNames.Length] } : p;

        private static (string Code, string Name) PickPayer(Random rng)
        {
            var roll = rng.NextDouble();
            foreach (var p in Payers)
            {
                if ((roll -= p.Share) <= 0) return (p.Code, p.Name);
            }
            return (Payers[0].Code, Payers[0].Name);
        }

        private static decimal Money(Random rng, double min, double max) => Math.Round((decimal)(min + rng.NextDouble() * (max - min)), 2);
    }
}
