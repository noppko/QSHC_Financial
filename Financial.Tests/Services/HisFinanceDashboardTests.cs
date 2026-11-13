using Financial.Models.Dashboard;
using Financial.Services.Dashboard;
using FluentAssertions;
using System.Data;

namespace Financial.Tests.Services
{
    /// <summary>
    /// ทดสอบการแปลงผล SQL ของ HIS เป็นตัวเลข Dashboard โดยไม่ต้องต่อฐานข้อมูล
    /// </summary>
    public class HisFinanceDashboardTests
    {
        private static readonly DateTime Now = new(2026, 10, 22, 10, 30, 0);
        private static readonly DateOnly Today = DateOnly.FromDateTime(Now);

        private static RevenueRow Row(DateOnly date, string side, bool credit, decimal amount, string payor = "B", int bills = 1)
            => new(date, side, credit, payor, payor == "B" ? "กรมบัญชีกลาง" : "สปสช", bills, amount);

        [Fact]
        public void Build_SplitsTodayMonthCashCreditAndPreviousMonthToDate()
        {
            var rows = new List<RevenueRow>
            {
                Row(Today, "O", false, 1_000),                    // วันนี้ OPD เงินสด
                Row(Today, "I", true, 5_000),                     // วันนี้ IPD เครดิต
                Row(new DateOnly(2026, 10, 1), "O", true, 2_000), // ต้นเดือน
                Row(new DateOnly(2026, 9, 22), "O", false, 700),  // เดือนก่อน วันเดียวกัน → นับ
                Row(new DateOnly(2026, 9, 23), "O", false, 900),  // เดือนก่อน หลังวันเดียวกัน → ไม่นับใน MTD
            };

            var d = FinanceDashboardBuilder.Build(Now, rows, [], null, [], []);

            d.RevenueToday.Opd.Should().Be(1_000);
            d.RevenueToday.Ipd.Should().Be(5_000);
            d.RevenueMonth.Total.Should().Be(8_000);
            d.CashBilledMonth.Total.Should().Be(1_000);
            d.CreditBilledMonth.Total.Should().Be(7_000);
            d.CreditBillsMonth.Should().Be(2);
            d.RevenuePreviousMonthToDate.Total.Should().Be(700, "เทียบเฉพาะช่วงวันที่ 1–22 ของเดือนก่อน");
            d.IsSample.Should().BeFalse();
            d.DataSource.Should().Be("HIS HealthObject");
        }

        [Fact]
        public void Build_DailySeriesHas30DaysIncludingEmptyDays()
        {
            var d = FinanceDashboardBuilder.Build(Now, [Row(Today.AddDays(-3), "I", true, 100)], [], null, [], []);

            d.DailyRevenue.Should().HaveCount(30);
            d.DailyRevenue[0].Date.Should().Be(Today.AddDays(-29));
            d.DailyRevenue[^1].Date.Should().Be(Today);
            d.DailyRevenue.Single(x => x.Date == Today.AddDays(-3)).Ipd.Should().Be(100);
            d.DailyRevenue.Where(x => x.Date != Today.AddDays(-3)).Should().OnlyContain(x => x.Opd == 0 && x.Ipd == 0);
        }

        [Fact]
        public void Build_GroupsSmallPayorCategoriesIntoOthers()
        {
            var rows = Enumerable.Range(0, 9)
                .Select(i => new RevenueRow(Today, "O", true, ((char)('A' + i)).ToString(), $"กลุ่ม {i}", 1, 1000 - i * 10))
                .ToList();

            var d = FinanceDashboardBuilder.Build(Now, rows, [], null, [], []);

            d.RevenueByPayer.Should().HaveCount(7);
            d.RevenueByPayer[0].Code.Should().Be("A");
            d.RevenueByPayer[^1].Name.Should().Be("อื่นๆ");
            d.RevenueByPayer.Sum(p => p.Total).Should().Be(rows.Sum(r => r.NetAmount));
        }

        [Fact]
        public void Build_AverageBillAndVolume()
        {
            var rows = new List<RevenueRow> { Row(Today, "O", false, 3_000, bills: 2), Row(Today, "I", true, 90_000, bills: 1) };

            var d = FinanceDashboardBuilder.Build(Now, rows, [], new VolumeRow(850, 92, 14), [], ["x"]);

            d.Volume.AvgBillOpd.Should().Be(1_500);
            d.Volume.AvgBillIpd.Should().Be(90_000);
            d.Volume.OpdVisitsToday.Should().Be(850);
            d.Volume.IpdAdmittedNow.Should().Be(92);
            d.Warnings.Should().Equal("x");
        }

        [Fact]
        public void ReadRevenueRows_MapsSqlColumns()
        {
            var table = new DataTable();
            table.Columns.Add("BillDate", typeof(DateTime));
            table.Columns.Add("Side", typeof(string));
            table.Columns.Add("IsCredit", typeof(int));
            table.Columns.Add("PayorCategoryCode", typeof(string));
            table.Columns.Add("PayorCategory", typeof(string));
            table.Columns.Add("Bills", typeof(int));
            table.Columns.Add("NetAmount", typeof(decimal));
            table.Rows.Add(new DateTime(2026, 10, 21), "I", 1, "A", "สปสช", 12, 345_678.90m);

            using var reader = table.CreateDataReader();
            var row = HisFinanceDashboardService.ReadRevenueRows(reader).Single();

            row.Should().Be(new RevenueRow(new DateOnly(2026, 10, 21), "I", true, "A", "สปสช", 12, 345_678.90m));
        }

        [Fact]
        public void ReadPending_ComputesWaitingMinutesAndHidesVisitUid()
        {
            var table = new DataTable();
            table.Columns.Add("VisitUid", typeof(long));
            table.Columns.Add("WaitingSince", typeof(DateTime));
            table.Columns.Add("InBilling", typeof(int));
            table.Columns.Add("LocationName", typeof(string));
            table.Columns.Add("PayorCategoryCode", typeof(string));
            table.Columns.Add("PayorCategory", typeof(string));
            table.Columns.Add("Amount", typeof(decimal));
            table.Rows.Add(987654321L, Now.AddMinutes(-25), 1, "คลินิกหัวใจ", "F", "เงินสด", 2_450.50m);
            table.Rows.Add(5L, DBNull.Value, 0, "-", "?", "(ไม่ระบุ)", 0m);

            using var reader = table.CreateDataReader();
            var rows = HisFinanceDashboardService.ReadPending(reader, "O", Now);

            rows[0].Should().Be(new PendingPayment("O", "#4321", "F", "เงินสด", "คลินิกหัวใจ", 2_450.50m, 25, true, VisitUid: 987654321L, Zone: "O"));
            rows[0].Hn.Should().BeNull("ข้อมูลที่อ่านจาก query ปกติต้องไม่มี HN/ชื่อ");
            rows[0].PatientName.Should().BeNull();
            rows[0].Reference.Should().NotContain("987654321", "ห้ามแสดง VisitUid เต็ม");
            rows[1].WaitingMinutes.Should().Be(0);
        }

        [Fact]
        public void ReadPending_DrugsMustBeScreenedFirst_WaitCountsFromLaterOfExamAndScreening()
        {
            var table = new DataTable();
            foreach (var c in new[] { "VisitUid", "WaitingSince", "InBilling", "LocationName", "LocationCode", "PayorCategoryCode", "PayorCategory", "Amount",
                                      "DrugItems", "DrugVerified", "DrugDispensed", "DrugVerifiedAt" })
                table.Columns.Add(c, c switch
                {
                    "VisitUid" => typeof(long), "WaitingSince" or "DrugVerifiedAt" => typeof(DateTime), "Amount" => typeof(decimal),
                    "InBilling" or "DrugItems" or "DrugVerified" or "DrugDispensed" => typeof(int), _ => typeof(string),
                });
            var exam = Now.AddMinutes(-40);
            table.Rows.Add(1L, exam, 0, "คลินิก", "D1", "F", "เงินสด", 100m, 0, 0, 0, DBNull.Value);                 // ไม่มียา → ชำระได้ทันที นับจากออกห้องตรวจ
            table.Rows.Add(2L, exam, 0, "คลินิก", "D1", "F", "เงินสด", 100m, 3, 0, 0, DBNull.Value);                 // มียา ยังไม่คัดกรอง → ยังไม่นับ
            table.Rows.Add(3L, exam, 0, "คลินิก", "D1", "F", "เงินสด", 100m, 3, 3, 0, Now.AddMinutes(-12));         // คัดกรองหลังออกห้องตรวจ → นับจากคัดกรอง
            table.Rows.Add(4L, exam, 1, "คลินิก", "D1", "F", "เงินสด", 100m, 2, 2, 0, Now.AddMinutes(-55));         // คัดกรองก่อนออกห้องตรวจ → นับจากออกห้องตรวจ
            table.Rows.Add(5L, exam, 0, "คลินิก", "D1", "F", "เงินสด", 100m, 2, 2, 1, Now.AddMinutes(-20));         // รับยาก่อนชำระ

            using var reader = table.CreateDataReader();
            var rows = HisFinanceDashboardService.ReadPending(reader, "O", Now);

            rows[0].Should().Match<PendingPayment>(p => p.DrugStage == null && p.WaitingMinutes == 40 && !p.WaitingScreening);
            rows[1].Should().Match<PendingPayment>(p => p.DrugStage == DrugStages.WAIT_SCREENING && p.WaitingMinutes == 0 && p.ScreeningMinutes == 40 && p.WaitingScreening);
            rows[2].Should().Match<PendingPayment>(p => p.DrugStage == DrugStages.SCREENED && p.WaitingMinutes == 12 && p.ScreeningMinutes == null);
            rows[3].Should().Match<PendingPayment>(p => p.DrugStage == DrugStages.IN_BILLING && p.WaitingMinutes == 40);
            rows[4].Should().Match<PendingPayment>(p => p.DrugStage == DrugStages.DISPENSED_EARLY && p.WaitingMinutes == 20);
            rows[1].DrugStageText.Should().Contain("คัดกรอง");
        }

        [Fact]
        public void PendingOpdSql_ReadsMedicationOrderLog()
        {
            var sql = typeof(HisFinanceDashboardService).Assembly.GetManifestResourceNames().Single(n => n.EndsWith("dashboard-pending-opd.sql"));
            using var stream = typeof(HisFinanceDashboardService).Assembly.GetManifestResourceStream(sql)!;
            var text = new StreamReader(stream).ReadToEnd();
            text.Should().Contain("ORDCTUID = 48581").And.Contain("'VERFD'").And.Contain("DrugVerifiedAt").And.Contain("WITH (NOLOCK)");
            text.Should().NotContainAny("INSERT", "UPDATE ", "DELETE", "EXEC");
        }

        [Fact]
        public void ReadPending_PremiumClinicCode_GoesToSeparateCounterRoom()
        {
            var table = new DataTable();
            foreach (var c in new[] { "VisitUid", "WaitingSince", "InBilling", "LocationName", "LocationCode", "PayorCategoryCode", "PayorCategory", "Amount" })
                table.Columns.Add(c, c switch { "VisitUid" => typeof(long), "WaitingSince" => typeof(DateTime), "InBilling" => typeof(int), "Amount" => typeof(decimal), _ => typeof(string) });
            table.Rows.Add(1L, Now.AddMinutes(-5), 0, "Premium Clinic", "d2305 ", "F", "เงินสด", 1_000m);
            table.Rows.Add(2L, Now.AddMinutes(-5), 0, "คลินิกหัวใจ", "D1101", "F", "เงินสด", 500m);
            table.Rows.Add(3L, Now.AddMinutes(-5), 0, "-", DBNull.Value, "F", "เงินสด", 500m);

            using var reader = table.CreateDataReader();
            var rows = HisFinanceDashboardService.ReadPending(reader, "O", Now, new HashSet<string>(["D2305"], StringComparer.OrdinalIgnoreCase));

            rows.Select(r => r.CounterZone).Should().Equal(PendingZones.PREMIUM, PendingZones.OPD, PendingZones.OPD);
            rows.Should().OnlyContain(r => r.Type == "O", "Premium ยังเป็นผู้ป่วยนอก แค่แยกห้องการเงิน");
        }

        [Theory]
        [InlineData("D2305", "คลินิกใดก็ได้", true)]
        [InlineData("X9999", "Premium Clinic", true)]
        [InlineData(null, "premium clinic (นอกเวลา)", true)]
        [InlineData("D1101", "คลินิกหัวใจ", false)]
        [InlineData(null, null, false)]
        public void IsPremium_MatchesCodeOrClinicName(string? code, string? name, bool expected)
            => PendingZones.IsPremium(code, name, new HashSet<string>(["D2305"])).Should().Be(expected);

        [Fact]
        public void PendingOpdQuery_TreatsExaminationCompletedAsWaiting()
        {
            var sql = HisQueries.PendingOpd;
            sql.Should().Contain("IN (28029, 1361)", "SEEEN 1361 = Examination Completed เริ่มรอชำระเงินด้วย");
            sql.Should().Contain("AS LocationCode");
        }

        [Fact]
        public void ReadCollections_TranslatesPaymentModes()
        {
            var table = new DataTable();
            foreach (var c in new[] { "PayMode", "Receipts", "Received", "Refunded", "Deposit", "NetCollected" }) table.Columns.Add(c, c == "PayMode" ? typeof(string) : typeof(decimal));
            table.Rows.Add("QRCODE", 120m, 350_000m, 1_000m, 0m, 349_000m);

            using var reader = table.CreateDataReader();
            var c0 = HisFinanceDashboardService.ReadCollections(reader).Single();

            c0.Should().Be(new PaymentModeAmount("QRCODE", "QR Code", 120, 350_000m, 1_000m, 0m, 349_000m));
        }

        [Theory]
        [InlineData("dashboard-revenue-daily.sql")]
        [InlineData("dashboard-collections.sql")]
        [InlineData("dashboard-volume.sql")]
        [InlineData("dashboard-pending-opd.sql")]
        [InlineData("dashboard-pending-ipd.sql")]
        [InlineData("dashboard-pending-identity.sql")]
        [InlineData("report-fiscal-payor.sql")]
        public void SqlFiles_AreEmbeddedAndReadOnly(string fileName)
        {
            var assembly = typeof(HisFinanceDashboardService).Assembly;
            var name = assembly.GetManifestResourceNames().Single(n => n.EndsWith(fileName));
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            var sql = reader.ReadToEnd();

            // เงื่อนไขเดียวกับ guard ของ tools/db.mjs: SELECT/WITH เดียว ไม่มีคำสั่งเขียนข้อมูล
            var code = System.Text.RegularExpressions.Regex.Replace(sql, @"--[^\n]*|'(?:[^']|'')*'", " ").Trim();
            code.Should().MatchRegex(@"^(?i)(select|with)\b");
            code.Should().NotMatchRegex(@"(?i)\b(insert|update|delete|merge|truncate|drop|alter|create|exec|execute|into|declare|set)\b");
            code.TrimEnd().TrimEnd(';').Should().NotContain(";");
            code.Should().Contain("NOLOCK", "ฐานเป็น replica ที่ใช้งานจริง ห้ามล็อกตาราง");
        }
    }
}
