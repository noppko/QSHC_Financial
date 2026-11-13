using Financial.Controllers;
using Financial.Models;
using Financial.Services;
using Financial.Services.Reports;
using Financial.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OfficeOpenXml;
using System.Data;
using System.Globalization;

namespace Financial.Tests.Services
{
    /// <summary>รายงานสิทธิ์ตามปีงบประมาณ (สปสช. / กรมบัญชีกลาง / ประกันสังคม)</summary>
    public class FiscalReportTests
    {
        private static readonly FiscalReportOptions Options = new();
        private const string OUT_OF_PROVINCE = "ค่าใช้จ่ายปกตินอกจังหวัดขอนแก่น";
        private const string IN_PROVINCE = "ค่าใช้จ่ายปกติในจังหวัดขอนแก่น";
        private static FiscalSchemeOptions Nhso => Options.Scheme(PayorSchemeCodes.NHSO)!;

        private static FiscalPatientRow Row(long patient, string payor = "A", string agreement = IN_PROVINCE, string? encounter = "INPAT", string billSide = "I",
            int visits = 1, decimal amount = 1_000m)
            => new(billSide, encounter, payor, agreement, patient, visits, visits, amount);

        private static FiscalYearData Aggregate(int year, params FiscalPatientRow[] rows)
        {
            var aggregator = new FiscalYearAggregator(year, Options);
            foreach (var r in rows) aggregator.Add(r);
            return aggregator.Result(new DateTime(2026, 11, 11, 2, 0, 0));
        }

        [Theory]
        [InlineData(2568, "2024-10-01", "2025-10-01")]
        [InlineData(2570, "2026-10-01", "2027-10-01")]
        public void FiscalYear_StartsOctoberFirst(int year, string start, string end)
        {
            var fy = new FiscalYear(year);
            fy.Start.Should().Be(DateTime.Parse(start, CultureInfo.InvariantCulture));
            fy.End.Should().Be(DateTime.Parse(end, CultureInfo.InvariantCulture));
        }

        [Theory]
        [InlineData("2025-09-30", 2568)]
        [InlineData("2025-10-01", 2569)]
        [InlineData("2026-10-22", 2570)]
        public void FiscalYear_Of_UsesOctoberBoundary(string date, int expected)
        {
            FiscalYear.Of(DateTime.Parse(date, CultureInfo.InvariantCulture)).BuddhistYear.Should().Be(expected);
        }

        [Theory]
        [InlineData(OUT_OF_PROVINCE, true)]
        [InlineData(IN_PROVINCE, false)]
        [InlineData("ส่งต่อในเขต 7", false)]
        [InlineData("ผู้ป่วยนอกเขต สปสช.", true)]
        [InlineData("สปสช. ทั่วไป", false)]
        [InlineData("(ไม่ระบุ)", false)]
        [InlineData(null, false)]
        [InlineData("ท.74 นอกขอนแก่น OPD", true)]
        [InlineData("ท.74 ผู้พิการในเขตจังหวัดขอนแก่น", false)]
        [InlineData("คลินิกนอกเวลา", false)]
        [InlineData("คลินิกนอกเวลา นอกจังหวัดขอนแก่น", true)]
        [InlineData("ผู้ป่วยนอก สปสช.", false)]
        [InlineData("ผู้ป่วยนอกจังหวัด", true)]
        public void Options_ClassifyOutOfZoneByAgreementName(string? name, bool expected)
        {
            Options.IsOutOfZone(name).Should().Be(expected);
        }

        [Theory]
        [InlineData("O", "INPAT", "I", FiscalReportOptions.TYPE_SOURCE_VISIT)]   // AN แม้บิล OPD → IPD
        [InlineData("I", "OUPAT", "O", FiscalReportOptions.TYPE_SOURCE_VISIT)]   // VN → OPD
        [InlineData("I", "AEPAT", "O", FiscalReportOptions.TYPE_SOURCE_VISIT)]   // ER → OPD
        [InlineData("O", "inpat", "I", FiscalReportOptions.TYPE_SOURCE_VISIT)]
        [InlineData("I", null, "I", FiscalReportOptions.TYPE_SOURCE_BILL)]       // ไม่มีประเภท visit → ตามบิล
        [InlineData("O", "UNKNOWN", "O", FiscalReportOptions.TYPE_SOURCE_BILL)]
        public void Options_ResolvePatientType_FromVisitThenBill(string billSide, string? encounter, string expectedType, string expectedSource)
        {
            Options.ResolvePatientType(billSide, encounter).Should().Be((expectedType, expectedSource));
        }

        [Theory]
        [InlineData("A", PayorSchemeCodes.NHSO)]
        [InlineData("B", PayorSchemeCodes.CSMBS)]
        [InlineData("V", PayorSchemeCodes.CSMBS)]
        [InlineData("C", PayorSchemeCodes.SSO)]
        [InlineData("O", PayorSchemeCodes.SSO)]
        [InlineData("P", PayorSchemeCodes.LGO)]
        [InlineData("W", PayorSchemeCodes.LGO)]
        [InlineData("F", null)]
        public void Options_DefaultSchemes_MapPayorCategories(string category, string? expected)
        {
            Options.SchemeOf(category).Should().Be(expected);
            Options.AllPayorCategoryCodes.Should().BeEquivalentTo(["A", "B", "V", "C", "N", "O", "P", "W"]);
        }

        [Fact]
        public void Aggregator_CountsDistinctPatients_NotVisits()
        {
            var data = Aggregate(2568,
                Row(1, visits: 3, amount: 300),                              // คนที่ 1 มา 3 visit
                Row(1, agreement: OUT_OF_PROVINCE, visits: 1, amount: 100),  // คนที่ 1 มีเงื่อนไขนอกเขตด้วย
                Row(2, visits: 2, amount: 200),
                Row(3, payor: "B", visits: 1, amount: 50),                   // กรมบัญชีกลาง
                Row(4, payor: "F", visits: 9, amount: 9));                   // เงินสด — ไม่อยู่ในรายงาน

            FiscalSchemeTotal T(string scheme, string zone) => data.Totals.Single(t => t.SchemeCode == scheme && t.PatientTypeCode == "I" && t.ZoneCode == zone);
            T(PayorSchemeCodes.NHSO, FiscalZoneCodes.IN).Should().BeEquivalentTo(new { Patients = 2, Visits = 5, NetAmount = 500m });
            T(PayorSchemeCodes.NHSO, FiscalZoneCodes.OUT).Should().BeEquivalentTo(new { Patients = 1, Visits = 1, NetAmount = 100m });
            T(PayorSchemeCodes.NHSO, FiscalZoneCodes.ALL).Should().BeEquivalentTo(new { Patients = 2, Visits = 6, NetAmount = 600m }, "คนเดียวกันนับครั้งเดียวในช่องรวม");
            T(PayorSchemeCodes.CSMBS, FiscalZoneCodes.ALL).Patients.Should().Be(1);
            data.Totals.Should().NotContain(t => t.SchemeCode == PayorSchemeCodes.SSO);
            data.SourceRows.Should().Be(5);
            data.Totals.Should().OnlyContain(t => t.FiscalYear == 2568);
        }

        [Theory]
        [InlineData("A", "สปสช(อปท)", "ค่าใช้จ่ายปกติในจังหวัดขอนแก่น", PayorSchemeCodes.LGO)]   // ผู้จ่าย อปท. อยู่กลุ่ม สปสช. ใน HIS
        [InlineData("A", "สปสช.", "อปท. นอกจังหวัด", PayorSchemeCodes.LGO)]                    // คำอยู่ในชื่อเงื่อนไข
        [InlineData("A", "สปสช.", "ค่าใช้จ่ายปกติในจังหวัดขอนแก่น", PayorSchemeCodes.NHSO)]
        [InlineData("?", "สปสช(อปท)", "x", PayorSchemeCodes.LGO)]                              // ไม่มีกลุ่มผู้จ่ายก็จับจากชื่อได้
        [InlineData("F", "เงินสด", "x", null)]
        public void Options_SchemeOf_PayorNameKeywordComesBeforeCategory(string category, string payorName, string agreement, string? expected)
        {
            Options.SchemeOf(category, payorName, agreement).Should().Be(expected);
        }

        [Fact]
        public void Aggregator_LgoPayor_CountedInLgo_NotTwiceInNhso()
        {
            var data = Aggregate(2569,
                new FiscalPatientRow("I", "INPAT", "A", IN_PROVINCE, 1, 1, 1, 1_000m, PayorName: "สปสช(อปท)"),
                new FiscalPatientRow("I", "INPAT", "A", IN_PROVINCE, 2, 1, 1, 2_000m, PayorName: "สปสช."));

            int Patients(string scheme) => data.Totals.Where(t => t.SchemeCode == scheme && t.ZoneCode == FiscalZoneCodes.ALL).Sum(t => t.Patients);
            Patients(PayorSchemeCodes.LGO).Should().Be(1);
            Patients(PayorSchemeCodes.NHSO).Should().Be(1, "สปสช(อปท) ไม่นับซ้ำใน สปสช.");
        }

        [Fact]
        public void HisSource_BuildSql_AddsParameterizedPayorNameFilter()
        {
            var sql = FiscalHisSource.BuildSql(2, 1);

            sql.Should().Contain("IN (@c0, @c1) OR PD.PayorName LIKE @k0 OR PA.Name LIKE @k0)");
            sql.Should().NotContain(FiscalHisSource.PAYOR_NAME_FILTER_PLACEHOLDER).And.Contain("AS PayorName");
            FiscalHisSource.BuildSql(2).Should().Contain("IN (@c0, @c1))", "ไม่มีคำ = ไม่เพิ่มเงื่อนไข");
            Options.AllPayorNameKeywords.Should().Equal("อปท");
        }

        [Fact]
        public void Build_ReportsKeywordRules_ForFootnote()
        {
            var lgo = FiscalReportBuilder.Build(Options.Scheme(PayorSchemeCodes.LGO)!, "อปท.", "I", [new FiscalYear(2569)], [], [], Options, DateTime.Now);
            var nhso = FiscalReportBuilder.Build(Nhso, "สปสช.", "I", [new FiscalYear(2569)], [], [], Options, DateTime.Now);

            lgo.PayorNameKeywords.Should().Equal("อปท");
            nhso.ExcludedPayorNameKeywords.Should().Equal("อปท");
        }

        [Fact]
        public void Aggregator_SplitsOpdIpdByVisitType_AndFlagsBillMismatch()
        {
            var data = Aggregate(2568,
                Row(1, encounter: "INPAT", billSide: "O"),   // AN ที่เรียกเก็บผ่านบิล OPD → IPD
                Row(2, encounter: "OUPAT", billSide: "O"),
                Row(3, encounter: null, billSide: "I"));     // ไม่มีประเภท visit → ตามบิล

            data.Totals.Single(t => t.PatientTypeCode == "I" && t.ZoneCode == FiscalZoneCodes.ALL).Patients.Should().Be(2);
            data.Totals.Single(t => t.PatientTypeCode == "O" && t.ZoneCode == FiscalZoneCodes.ALL).Patients.Should().Be(1);
            data.Agreements.Single(a => a.PatientTypeCode == "I" && a.VisitEncounterCode == "INPAT").MovedFromBillSide.Should().BeTrue();
            data.Agreements.Single(a => a.VisitEncounterCode == "OUPAT").MovedFromBillSide.Should().BeFalse();
        }

        [Fact]
        public void Build_ComputesPercents_AndSumsYears()
        {
            var totals = Aggregate(2567, Row(1, amount: 600_000), Row(2, agreement: OUT_OF_PROVINCE, amount: 400_000)).Totals
                .Concat(Aggregate(2568, Row(1, amount: 1_000_000)).Totals).ToList();

            var report = FiscalReportBuilder.Build(Nhso, "สปสช.", "I", [new FiscalYear(2567), new FiscalYear(2568)], totals, [], Options, DateTime.Now);

            report.Rows[0].InZone.Should().Be(new ZoneFigure(1, 1, 600_000, 60));
            report.Rows[0].OutZone.Should().Be(new ZoneFigure(1, 1, 400_000, 40));
            report.Rows[0].Total.Should().Be(new ZoneFigure(2, 2, 1_000_000, 50), "ปีนี้ = ครึ่งหนึ่งของยอดทุกปี");
            report.Rows[1].OutZone.Patients.Should().Be(0);
            report.Sum.Total.Should().Be(new ZoneFigure(3, 3, 2_000_000, 100), "แถวรวม = ผลรวมรายปี");
            report.SchemeName.Should().Be("สปสช.");
        }

        [Fact]
        public void Build_NoData_ReturnsZerosWithoutDivideByZero()
        {
            var report = FiscalReportBuilder.Build(Nhso, "สปสช.", "O", [new FiscalYear(2569)], [], [], Options, DateTime.Now, ["x"]);

            report.Rows.Single().Total.Should().Be(new ZoneFigure(0, 0, 0, 0));
            report.Warnings.Should().Equal("x");
            report.SideName.Should().Contain("OPD");
        }

        [Fact]
        public void HisSource_ReadRows_MapsSqlColumns_AndBuildsCategoryParameters()
        {
            var table = new DataTable();
            foreach (var (name, type) in new[] { ("BillSide", typeof(string)), ("VisitEncounterCode", typeof(string)), ("PayorCategoryCode", typeof(string)),
                ("AgreementName", typeof(string)), ("PatientUid", typeof(long)), ("Visits", typeof(int)), ("Bills", typeof(int)), ("NetAmount", typeof(decimal)) })
            {
                table.Columns.Add(name, type);
            }
            table.Rows.Add("I", "INPAT ", "A", OUT_OF_PROVINCE + " ", 11L, 2, 3, 1234.5m);
            table.Rows.Add("O", DBNull.Value, "A", DBNull.Value, 12L, 1, 1, DBNull.Value);

            var aggregator = new FiscalYearAggregator(2568, Options);
            using (var reader = table.CreateDataReader()) FiscalHisSource.ReadRows(reader, aggregator);
            var data = aggregator.Result(DateTime.Now);

            data.SourceRows.Should().Be(2);
            data.Agreements.Should().ContainSingle(a => a.AgreementName == OUT_OF_PROVINCE && a.VisitEncounterCode == "INPAT" && a.Patients == 1 && a.Visits == 2 && a.NetAmount == 1234.5m);
            data.Agreements.Should().ContainSingle(a => a.AgreementName == "(ไม่ระบุ)" && a.PatientTypeCode == "O");

            var sql = FiscalHisSource.BuildSql(3);
            sql.Should().Contain("IN (@c0, @c1, @c2)").And.NotContain(FiscalHisSource.PAYOR_CATEGORIES_PLACEHOLDER);
            sql.Should().Contain("PV.ENTYPUID", "แยก OPD/IPD ตามประเภท visit");
        }

        [Fact]
        public async Task Refresher_ReplacesRowsOfThatYearOnly_InFinancialDatabase()
        {
            using var db = TestHost.CreateInMemoryDb();
            db.FiscalSchemeTotals.AddRange(
                new FiscalSchemeTotal { FiscalYear = 2568, SchemeCode = "NHSO", PatientTypeCode = "I", ZoneCode = "ALL", Patients = 99, RefreshedAt = DateTime.Now.AddDays(-1) },
                new FiscalSchemeTotal { FiscalYear = 2567, SchemeCode = "NHSO", PatientTypeCode = "I", ZoneCode = "ALL", Patients = 7, RefreshedAt = DateTime.Now.AddDays(-1) });
            db.FiscalPayorSummaries.Add(new FiscalPayorSummary { FiscalYear = 2568, SchemeCode = "NHSO", PatientTypeCode = "I", PayorCategoryCode = "A", AgreementName = "เก่า", RefreshedAt = DateTime.Now.AddDays(-1) });
            await db.SaveChangesAsync();

            var his = new Mock<IFiscalHisSource>();
            his.Setup(h => h.LoadYearAsync(new FiscalYear(2568), It.IsAny<IProgress<int>?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(Aggregate(2568, Row(1, amount: 400_000m), Row(2, payor: "C", encounter: "OUPAT", billSide: "O", amount: 25_000m)));

            var refresher = new FiscalSummaryRefresher(db, his.Object, NullLogger<FiscalSummaryRefresher>.Instance);
            var count = await refresher.RefreshAsync(new FiscalYear(2568));

            count.Should().Be(2);
            var totals = await db.FiscalSchemeTotals.AsNoTracking().ToListAsync();
            totals.Should().Contain(t => t.FiscalYear == 2567 && t.Patients == 7, "ปีอื่นต้องไม่ถูกแตะ");
            totals.Where(t => t.FiscalYear == 2568).Select(t => t.SchemeCode).Distinct().Should().BeEquivalentTo(["NHSO", "SSO"]);
            totals.Should().NotContain(t => t.Patients == 99);
            (await db.FiscalPayorSummaries.AsNoTracking().ToListAsync()).Should().NotContain(a => a.AgreementName == "เก่า");
            totals.First(t => t.FiscalYear == 2568).CreatedBy.Should().Be("System");
        }

        [Fact]
        public async Task ReportService_ReadsFinancialDatabase_ForScheme_AndWarnsMissingYears()
        {
            using var db = TestHost.CreateInMemoryDb();
            var data = Aggregate(2568, Row(1, agreement: OUT_OF_PROVINCE, amount: 800m), Row(2, payor: "B", amount: 5m));
            db.FiscalSchemeTotals.AddRange(data.Totals);
            db.FiscalPayorSummaries.AddRange(data.Agreements);
            await db.SaveChangesAsync();

            var names = new Mock<IReferenceValueService>();
            names.Setup(n => n.GetNameAsync(ReferenceCategories.PAYOR_SCHEME, It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string _, string _, string fallback) => fallback);
            var service = new FiscalReportService(db, Microsoft.Extensions.Options.Options.Create(new FiscalReportOptions()), names.Object,
                new FixedLocalTimeProvider(new DateTime(2026, 10, 22, 10, 0, 0)), NullLogger<FiscalReportService>.Instance);

            var nhso = await service.GetAsync(PayorSchemeCodes.NHSO, PatientTypeCodes.IPD, [new FiscalYear(2568), new FiscalYear(2569)]);
            nhso.Rows[0].OutZone.Should().Be(new ZoneFigure(1, 1, 800m, 100));
            nhso.RefreshedAt.Should().Be(new DateTime(2026, 11, 11, 2, 0, 0));
            nhso.Warnings.Should().ContainSingle().Which.Should().Contain("2569");
            nhso.Agreements.Should().ContainSingle().Which.Patients.Should().Be(1);

            var csmbs = await service.GetAsync(PayorSchemeCodes.CSMBS, PatientTypeCodes.IPD, [new FiscalYear(2568)]);
            csmbs.Sum.Total.Amount.Should().Be(5m);
            csmbs.SchemeName.Should().Be("กรมบัญชีกลาง");
            csmbs.Warnings.Should().BeEmpty();
        }

        [Theory]
        [InlineData("2026-10-22", new[] { 2570, 2569 })]
        [InlineData("2027-01-05", new[] { 2570 })]
        public void Worker_RefreshesPreviousYearOnlyInOctToDec(string date, int[] expected)
        {
            FiscalRefreshWorker.YearsToRefresh(DateTime.Parse(date, CultureInfo.InvariantCulture)).Should().Equal(expected);
        }

        [Fact]
        public void Queue_DoesNotDuplicateYearAlreadyQueued()
        {
            var queue = new FiscalRefreshQueue();

            queue.Enqueue(2568).Should().BeTrue();
            queue.Enqueue(2568).Should().BeFalse();
            queue.Get(2568)!.State.Should().Be(RefreshStatus.QUEUED);

            queue.SetStatus(new RefreshStatus(2568, RefreshStatus.DONE, DateTime.Now));
            queue.Enqueue(2568).Should().BeTrue("เสร็จแล้วสั่งใหม่ได้");
        }

        [Fact]
        public void Controller_Normalize_UsesOnlyYearsWithData_WithoutLimit()
        {
            var time = new FixedLocalTimeProvider(new DateTime(2026, 10, 22, 10, 0, 0)); // ปีงบปัจจุบัน 2570
            var controller = new ReportsController(Mock.Of<IFiscalReportService>(),
                Microsoft.Extensions.Options.Options.Create(new FiscalReportOptions()), time, NullLogger<ReportsController>.Instance);
            int[] available = [2569, 2560, 2561, 2562, 2563, 2564, 2565, 2566, 2568, 2570]; // ไม่มี 2567

            var (side, years, error) = controller.Normalize(null, null, null, available);
            side.Should().Be("I");
            years.Select(y => y.BuddhistYear).Should().Equal(2560, 2561, 2562, 2563, 2564, 2565, 2566, 2568, 2569, 2570);
            error.Should().BeNull("ค่าเริ่มต้น = ทุกปีที่มีข้อมูล ไม่จำกัดจำนวนปี");

            var (side2, years2, _) = controller.Normalize("o", 2570, 2565, available);
            side2.Should().Be("O");
            years2.Select(y => y.BuddhistYear).Should().Equal([2565, 2566, 2568, 2569, 2570], "ข้ามปีที่ไม่มีข้อมูล และสลับช่วงที่กลับด้าน");

            var (_, years3, error3) = controller.Normalize(null, 2550, 2555, available);
            years3.Should().HaveCount(available.Length);
            error3.Should().Contain("ไม่มีข้อมูล");

            var (_, empty, _) = controller.Normalize(null, null, null, []);
            empty.Select(y => y.BuddhistYear).Should().Equal([2569], "ยังไม่มีข้อมูลเลย = ปีงบที่ปิดล่าสุด");
        }

        [Fact]
        public void OnlyNhso_SplitsZones_OtherSchemesShowTotalOnly()
        {
            var totals = Aggregate(2568, Row(1, payor: "B", agreement: OUT_OF_PROVINCE, amount: 500m), Row(2, payor: "A", amount: 100m)).Totals;
            var csmbsScheme = Options.Scheme(PayorSchemeCodes.CSMBS)!;

            Nhso.HasZones.Should().BeTrue();
            csmbsScheme.HasZones.Should().BeFalse();
            new FiscalSchemeOptions { Code = "SSO", SplitZones = true }.HasZones.Should().BeTrue("ตั้งค่าเปิดได้");

            var report = FiscalReportBuilder.Build(csmbsScheme, "กรมบัญชีกลาง", "I", [new FiscalYear(2568)], totals, [], Options, DateTime.Now);
            report.GroupNames.Should().Equal("");
            report.Figures(report.Rows[0]).Should().ContainSingle().Which.Amount.Should().Be(500m);

            using var package = new ExcelPackage(new MemoryStream(FiscalExcel.Create(report)));
            var ws = package.Workbook.Worksheets[0];
            // หัวชั้นเดียว: แถว 2 = หัวคอลัมน์, แถว 3 = ปี, แถว 4 = รวมทุกปี
            ws.Cells["A2"].Text.Should().Be("ปีงบประมาณ");
            ws.Cells["B2"].Text.Should().Be("จำนวน (ราย)");
            ws.Cells["C2"].Text.Should().Be("จำนวนเงิน (บาท)");
            ws.Cells["A3"].Text.Should().Be("2568");
            ws.Cells["B3"].GetValue<int>().Should().Be(1);
            ws.Cells["C3"].GetValue<decimal>().Should().Be(500m);
            ws.Cells["A4"].Text.Should().Be("รวม");
        }

        [Fact]
        public async Task ReportService_AvailableYears_ForScheme_ElseAnyProcessedYear()
        {
            using var db = TestHost.CreateInMemoryDb();
            db.FiscalSchemeTotals.AddRange(Aggregate(2568, Row(1, amount: 1m)).Totals);
            db.FiscalSchemeTotals.AddRange(Aggregate(2566, Row(1, amount: 1m), Row(2, payor: "B", amount: 1m)).Totals);
            await db.SaveChangesAsync();
            var service = new FiscalReportService(db, Microsoft.Extensions.Options.Options.Create(new FiscalReportOptions()), Mock.Of<IReferenceValueService>(),
                new FixedLocalTimeProvider(new DateTime(2026, 10, 22, 10, 0, 0)), NullLogger<FiscalReportService>.Instance);

            (await service.GetAvailableYearsAsync(PayorSchemeCodes.NHSO)).Should().Equal(2566, 2568);
            (await service.GetAvailableYearsAsync(PayorSchemeCodes.CSMBS)).Should().Equal(2566);
            (await service.GetAvailableYearsAsync(PayorSchemeCodes.LGO)).Should().Equal([2566, 2568], "สิทธิ์ที่ยังไม่มีข้อมูล ใช้ปีที่ประมวลผลแล้ว");
        }

        [Fact]
        public void Excel_WithVisits_AddsVisitColumnNextToPatients()
        {
            var totals = Aggregate(2568, Row(1, agreement: OUT_OF_PROVINCE, visits: 5, amount: 500m), Row(2, agreement: OUT_OF_PROVINCE, amount: 0m)).Totals;
            var report = FiscalReportBuilder.Build(Nhso, "สปสช.", "I", [new FiscalYear(2568)], totals, [], Options, DateTime.Now);

            using var package = new ExcelPackage(new MemoryStream(FiscalExcel.Create(report, showVisits: true)));
            var ws = package.Workbook.Worksheets[0];

            // 4 คอลัมน์ต่อกลุ่ม: ในเขต B–E, นอกเขต F–I, รวม J–M
            ws.Cells["F2"].Text.Should().Be("นอกเขต");
            ws.Cells["F3"].Text.Should().Be("จำนวน (ราย)");
            ws.Cells["G3"].Text.Should().Be("จำนวน (visit)");
            ws.Cells["H3"].Text.Should().Be("จำนวนเงิน (บาท)");
            ws.Cells["F4"].GetValue<int>().Should().Be(2);
            ws.Cells["G4"].GetValue<int>().Should().Be(6, "visit = 5 + 1");
            ws.Cells["H4"].GetValue<decimal>().Should().Be(500m);
            ws.Cells["J2"].Text.Should().Be("รวม");
        }

        [Fact]
        public void Excel_HasTwoLevelHeader_PatientsAndSumRow()
        {
            var totals = Aggregate(2568, Row(1, agreement: OUT_OF_PROVINCE, visits: 5, amount: 500m), Row(2, agreement: OUT_OF_PROVINCE, amount: 0m)).Totals;
            var report = FiscalReportBuilder.Build(Nhso, "สปสช.", "I", [new FiscalYear(2568)], totals, [], Options, DateTime.Now);

            using var package = new ExcelPackage(new MemoryStream(FiscalExcel.Create(report)));
            var ws = package.Workbook.Worksheets[0];

            ws.Cells["A1"].Text.Should().StartWith("สปสช. IPD");
            ws.Cells["A2"].Text.Should().Be("ปีงบประมาณ");
            ws.Cells["B2"].Text.Should().Be("ในเขต");
            ws.Cells["E2"].Text.Should().Be("นอกเขต");
            ws.Cells["H2"].Text.Should().Be("รวม");
            ws.Cells["A4"].Text.Should().Be("2568");
            ws.Cells["E4"].GetValue<int>().Should().Be(2, "จำนวน (ราย) = คน ไม่ใช่ visit");
            ws.Cells["F4"].GetValue<decimal>().Should().Be(500m);
            ws.Cells["A5"].Text.Should().Be("รวม");
        }
    }
}
