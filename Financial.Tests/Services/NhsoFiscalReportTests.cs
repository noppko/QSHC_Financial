using Financial.Controllers;
using Financial.Models;
using Financial.Services.Reports;
using Financial.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OfficeOpenXml;
using System.Data;
using System.Globalization;

namespace Financial.Tests.Services
{
    public class NhsoFiscalReportTests
    {
        private static readonly NhsoReportOptions Options = new() { PayorCategoryCodes = ["A"], OutOfZoneKeywords = ["นอก"] };
        private const string OUT_OF_PROVINCE = "ค่าใช้จ่ายปกตินอกจังหวัดขอนแก่น";
        private const string IN_PROVINCE = "ค่าใช้จ่ายปกติในจังหวัดขอนแก่น";

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
        [InlineData("ผู้ป่วยนอกเขต สปสช.", true)]           // มีคำว่า "นอก" = นอกเขต
        [InlineData("สปสช. ทั่วไป", false)]                  // ไม่มีทั้ง "ใน" และ "นอก" = ในเขตไว้ก่อน
        [InlineData("(ไม่ระบุ)", false)]
        [InlineData(null, false)]
        // ชื่อจริงจากรายงาน
        [InlineData("ท.74 นอกขอนแก่น OPD", true)]
        [InlineData("ท.74 ในขอนแก่น OPD", false)]
        [InlineData("ท.74 ผู้พิการนอกเขตจังหวัดขอนแก่น", true)]
        [InlineData("ท.74 ผู้พิการในเขตจังหวัดขอนแก่น", false)]
        [InlineData("ฉุกเฉินนอกขอนแก่น", true)]
        [InlineData("ท.74 IPD", false)]
        // "นอกเวลา" ไม่ใช่นอกเขต — ดูจากส่วนที่เหลือของชื่อสิทธิ์
        [InlineData("คลินิกนอกเวลา", false)]
        [InlineData("คลินิกนอกเวลา ในจังหวัดขอนแก่น", false)]
        [InlineData("คลินิกนอกเวลา นอกจังหวัดขอนแก่น", true)]
        // "ผู้ป่วยนอก" (OPD) ไม่ใช่นอกเขต แต่ "ผู้ป่วยนอกจังหวัด" ใช่
        [InlineData("ผู้ป่วยนอก สปสช.", false)]
        [InlineData("ผู้ป่วยนอกจังหวัด", true)]
        public void Options_ClassifyOutOfZoneByAgreementName(string? name, bool expected)
        {
            Options.IsOutOfZone(name).Should().Be(expected);
        }

        [Theory]
        [InlineData("O", "ท.74 IPD", "INPAT", "I", NhsoReportOptions.TYPE_SOURCE_AGREEMENT)]      // สิทธิ์ INPAT → IPD
        [InlineData("I", "สิทธิ์ฉุกเฉิน", "AEPAT", "O", NhsoReportOptions.TYPE_SOURCE_AGREEMENT)]  // สิทธิ์ ER (AEPAT) → OPD
        [InlineData("I", "ท.74 IPD", "UNKNOWN", "I", NhsoReportOptions.TYPE_SOURCE_NAME)]         // รหัสที่ไม่รู้จัก → ดูชื่อ
        [InlineData("O", "ท.74 IPD", null, "I", NhsoReportOptions.TYPE_SOURCE_NAME)]              // สิทธิ์ไม่ระบุ → ดูชื่อ
        [InlineData("I", "ท.74 ในขอนแก่น OPD", null, "O", NhsoReportOptions.TYPE_SOURCE_NAME)]
        [InlineData("I", "ท.74 ในขอนแก่น OPD", "INPAT", "I", NhsoReportOptions.TYPE_SOURCE_AGREEMENT)] // สิทธิ์มาก่อนชื่อ
        [InlineData("O", "ท.74 IPD", "inpat", "I", NhsoReportOptions.TYPE_SOURCE_AGREEMENT)]      // ไม่สนตัวพิมพ์เล็ก/ใหญ่
        [InlineData("O", OUT_OF_PROVINCE, null, "O", NhsoReportOptions.TYPE_SOURCE_BILL)]          // ไม่ระบุทั้งคู่ → ตามบิล
        [InlineData("I", "สิทธิ IPD/OPD", null, "I", NhsoReportOptions.TYPE_SOURCE_BILL)]          // ชื่อระบุทั้งสอง → ตามบิล
        public void Options_ResolvePatientType_AgreementThenNameThenBill(string billSide, string name, string? agreementType, string expectedType, string expectedSource)
        {
            Options.ResolvePatientType(billSide, name, agreementType).Should().Be((expectedType, expectedSource));
        }

        [Fact]
        public void Build_MovesRowsToPatientTypeOfAgreement_AndFlagsThem()
        {
            var rows = new List<NhsoAgreementRow>
            {
                new(2568, "O", "A", "ท.74 IPD", 3, 3, 30_000m, AgreementEncounterCode: "INPAT"),   // บิล OPD แต่สิทธิ์เป็น INPAT
                new(2568, "I", "A", OUT_OF_PROVINCE, 5, 5, 500_000m),
                new(2568, "O", "A", "ท.74 นอกขอนแก่น OPD", 9, 9, 900m, AgreementEncounterCode: "OUPAT"),
            };

            var ipd = NhsoFiscalReportBuilder.Build("I", [new FiscalYear(2568)], rows, Options, DateTime.Now);
            var opd = NhsoFiscalReportBuilder.Build("O", [new FiscalYear(2568)], rows, Options, DateTime.Now);

            ipd.Sum.Total.Should().Be(new ZoneFigure(8, 530_000m, 100));
            var moved = ipd.Agreements.Should().ContainSingle(a => a.AgreementName == "ท.74 IPD").Subject;
            moved.MovedFromBillSide.Should().BeTrue();
            moved.TypeSource.Should().Be(NhsoReportOptions.TYPE_SOURCE_AGREEMENT);
            moved.EncounterCode.Should().Be("INPAT");
            opd.Agreements.Should().NotContain(a => a.AgreementName.Contains("IPD"), "รายงาน OPD ต้องไม่มีสิทธิ์ประเภท I");
            opd.Sum.Total.Amount.Should().Be(900m);

            var billOnly = new NhsoReportOptions { PayorCategoryCodes = ["A"], PatientTypeFromAgreementName = false };
            NhsoFiscalReportBuilder.Build("O", [new FiscalYear(2568)], rows, billOnly, DateTime.Now).Sum.Total.Amount.Should().Be(30_900m, "ปิดกฎ = ใช้ประเภทบิลอย่างเดียว");
        }

        [Fact]
        public void Build_SplitsInOutZone_ComputesPercents_AndIgnoresOtherSideAndCategories()
        {
            var rows = new List<NhsoAgreementRow>
            {
                new(2567, "I", "A", IN_PROVINCE, 30, 30, 600_000),
                new(2567, "I", "A", OUT_OF_PROVINCE, 10, 10, 400_000),
                new(2568, "I", "A", IN_PROVINCE, 20, 20, 1_000_000),
                new(2568, "O", "A", IN_PROVINCE, 999, 999, 9_999),     // OPD — ไม่นับในรายงาน IPD
                new(2568, "I", "E", OUT_OF_PROVINCE, 5, 5, 50_000),     // กลุ่ม E ไม่ได้ตั้งค่าให้นับ
            };

            var report = NhsoFiscalReportBuilder.Build("I", [new FiscalYear(2567), new FiscalYear(2568)], rows, Options, DateTime.Now);

            var y2567 = report.Rows[0];
            y2567.InZone.Should().Be(new ZoneFigure(30, 600_000, 60));
            y2567.OutZone.Should().Be(new ZoneFigure(10, 400_000, 40));
            y2567.Total.Should().Be(new ZoneFigure(40, 1_000_000, 50), "ปีนี้ = ครึ่งหนึ่งของยอดทุกปี");

            report.Rows[1].OutZone.Visits.Should().Be(0);
            report.Sum.Total.Should().Be(new ZoneFigure(60, 2_000_000, 100));
            report.Sum.InZone.Amount.Should().Be(1_600_000);

            // ตารางตรวจการจัดกลุ่ม แสดงรายการที่ไม่ถูกนับด้วย
            report.Agreements.Should().Contain(a => a.PayorCategoryCode == "E" && !a.Counted);
            report.Agreements.Should().NotContain(a => a.Visits == 999, "ไม่รวม OPD ในรายงาน IPD");
        }

        [Fact]
        public void Build_NoData_ReturnsZerosWithoutDivideByZero()
        {
            var report = NhsoFiscalReportBuilder.Build("O", [new FiscalYear(2569)], [], Options, DateTime.Now, ["x"]);

            report.Rows.Single().Total.Should().Be(new ZoneFigure(0, 0, 0));
            report.Warnings.Should().Equal("x");
            report.SideName.Should().Contain("OPD");
        }

        [Fact]
        public void HisSource_ReadRows_MapsSqlColumns()
        {
            var table = new DataTable();
            table.Columns.Add("Side", typeof(string));
            table.Columns.Add("PayorCategoryCode", typeof(string));
            table.Columns.Add("AgreementName", typeof(string));
            table.Columns.Add("Visits", typeof(int));
            table.Columns.Add("Bills", typeof(int));
            table.Columns.Add("NetAmount", typeof(decimal));
            table.Columns.Add("AgreementEncounterCode", typeof(string));
            table.Rows.Add("I", "A", OUT_OF_PROVINCE + " ", 12, 13, 1234.5m, "INPAT ");
            table.Rows.Add("O", DBNull.Value, DBNull.Value, 3, 3, DBNull.Value, DBNull.Value);

            using var reader = table.CreateDataReader();
            var rows = NhsoHisSource.ReadRows(reader, 2568);

            rows[0].Should().Be(new NhsoAgreementRow(2568, "I", "A", OUT_OF_PROVINCE, 12, 13, 1234.5m, "INPAT"));
            rows[1].Should().Be(new NhsoAgreementRow(2568, "O", "?", "(ไม่ระบุ)", 3, 3, 0m, null));
        }

        [Fact]
        public async Task Refresher_ReplacesRowsOfThatYearOnly_InFinancialDatabase()
        {
            using var db = TestHost.CreateInMemoryDb();
            db.NhsoFiscalSummaries.AddRange(
                new NhsoFiscalSummary { FiscalYear = 2568, PatientTypeCode = PatientTypeCodes.IPD, PayorCategoryCode = "A", AgreementName = "เก่า", Visits = 1, NetAmount = 1, RefreshedAt = DateTime.Now.AddDays(-1) },
                new NhsoFiscalSummary { FiscalYear = 2567, PatientTypeCode = PatientTypeCodes.IPD, PayorCategoryCode = "A", AgreementName = "ปีอื่น", Visits = 9, NetAmount = 9, RefreshedAt = DateTime.Now.AddDays(-1) });
            await db.SaveChangesAsync();

            var his = new Mock<INhsoHisSource>();
            his.Setup(h => h.LoadYearAsync(new FiscalYear(2568), It.IsAny<CancellationToken>())).ReturnsAsync(
            [
                new NhsoAgreementRow(2568, "I", "A", OUT_OF_PROVINCE, 10, 11, 400_000m, "INPAT"),
                new NhsoAgreementRow(2568, "O", "A", IN_PROVINCE, 50, 50, 25_000m),
            ]);

            var refresher = new NhsoFiscalSummaryRefresher(db, his.Object, new MemoryCache(new MemoryCacheOptions()), NullLogger<NhsoFiscalSummaryRefresher>.Instance);
            var count = await refresher.RefreshAsync(new FiscalYear(2568));

            count.Should().Be(2);
            var all = await db.NhsoFiscalSummaries.AsNoTracking().ToListAsync();
            all.Where(r => r.FiscalYear == 2568).Select(r => r.AgreementName).Should().BeEquivalentTo([OUT_OF_PROVINCE, IN_PROVINCE]);
            all.Should().Contain(r => r.FiscalYear == 2567 && r.AgreementName == "ปีอื่น", "ปีอื่นต้องไม่ถูกแตะ");
            var ipd = all.Single(r => r.FiscalYear == 2568 && r.PatientTypeCode == PatientTypeCodes.IPD);
            ipd.CreatedBy.Should().Be("System");
            ipd.AgreementEncounterCode.Should().Be("INPAT", "เก็บรหัสดิบของประเภทสิทธิ์จาก HIS ไว้");
            all.Single(r => r.FiscalYear == 2568 && r.PatientTypeCode == PatientTypeCodes.OPD).AgreementEncounterCode.Should().BeNull();
            ipd.RefreshedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1), "เวลาไทย (DateTime.Now)");
        }

        [Fact]
        public async Task ReportService_SummaryMode_ReadsFinancialDatabase_AndWarnsMissingYears()
        {
            using var db = TestHost.CreateInMemoryDb();
            db.NhsoFiscalSummaries.Add(new NhsoFiscalSummary { FiscalYear = 2568, PatientTypeCode = PatientTypeCodes.IPD, PayorCategoryCode = "A", AgreementName = OUT_OF_PROVINCE, Visits = 4, Bills = 4, NetAmount = 800m, RefreshedAt = new DateTime(2026, 10, 22, 2, 0, 0) });
            await db.SaveChangesAsync();

            var his = new Mock<INhsoHisSource>(MockBehavior.Strict); // โหมด Summary ต้องไม่เรียก HIS
            var service = new NhsoFiscalReportService(db, his.Object, Microsoft.Extensions.Options.Options.Create(new NhsoReportOptions { PayorCategoryCodes = ["A"], OutOfZoneKeywords = ["นอก"] }),
                new MemoryCache(new MemoryCacheOptions()), new FixedLocalTimeProvider(new DateTime(2026, 10, 22, 10, 0, 0)), NullLogger<NhsoFiscalReportService>.Instance);

            var report = await service.GetAsync(PatientTypeCodes.IPD, [new FiscalYear(2568), new FiscalYear(2569)]);

            report.Rows[0].OutZone.Should().Be(new ZoneFigure(4, 800m, 100));
            report.RefreshedAt.Should().Be(new DateTime(2026, 10, 22, 2, 0, 0));
            report.Warnings.Should().ContainSingle().Which.Should().Contain("2569");
        }

        [Theory]
        [InlineData("2026-10-22", new[] { 2570, 2569 })]
        [InlineData("2027-01-05", new[] { 2570 })]
        public void Worker_RefreshesPreviousYearOnlyInOctToDec(string date, int[] expected)
        {
            NhsoFiscalRefreshWorker.YearsToRefresh(DateTime.Parse(date, CultureInfo.InvariantCulture)).Should().Equal(expected);
        }

        [Fact]
        public void Queue_DoesNotDuplicateYearAlreadyQueued()
        {
            var queue = new NhsoFiscalRefreshQueue();

            queue.Enqueue(2568).Should().BeTrue();
            queue.Enqueue(2568).Should().BeFalse();
            queue.Statuses.Single().State.Should().Be(RefreshStatus.QUEUED);
        }

        [Fact]
        public void Controller_Normalize_DefaultsToLastThreeClosedYears_AndLimitsRange()
        {
            var time = new FixedLocalTimeProvider(new DateTime(2026, 10, 22, 10, 0, 0)); // ปีงบปัจจุบัน 2570
            var controller = new ReportsController(Mock.Of<INhsoFiscalReportService>(), time);

            var (side, years, error) = controller.Normalize(null, null, null);
            side.Should().Be("I");
            years.Select(y => y.BuddhistYear).Should().Equal(2567, 2568, 2569);
            error.Should().BeNull();

            var (side2, years2, error2) = controller.Normalize("o", 2550, 2570);
            side2.Should().Be("O");
            years2.Should().HaveCount(ReportsController.MAX_YEARS);
            years2[^1].BuddhistYear.Should().Be(2570);
            error2.Should().NotBeNull();
        }

        [Fact]
        public void Excel_HasTwoLevelHeaderAndSumRow()
        {
            var rows = new List<NhsoAgreementRow> { new(2568, "I", "A", OUT_OF_PROVINCE, 2, 2, 500m) };
            var report = NhsoFiscalReportBuilder.Build("I", [new FiscalYear(2568)], rows, Options, DateTime.Now);

            using var package = new ExcelPackage(new MemoryStream(NhsoFiscalExcel.Create(report)));
            var ws = package.Workbook.Worksheets[0];

            ws.Cells["A2"].Text.Should().Be("ปีงบประมาณ");
            ws.Cells["B2"].Text.Should().Be("ในเขต");
            ws.Cells["E2"].Text.Should().Be("นอกเขต");
            ws.Cells["H2"].Text.Should().Be("รวม");
            ws.Cells["A4"].Text.Should().Be("2568");
            ws.Cells["E4"].GetValue<int>().Should().Be(2);
            ws.Cells["F4"].GetValue<decimal>().Should().Be(500m);
            ws.Cells["A5"].Text.Should().Be("รวม");
        }
    }
}
