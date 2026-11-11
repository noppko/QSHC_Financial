using Financial.Controllers;
using Financial.Models;
using Financial.Models.ViewModels;
using Financial.Services.Discharges;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Financial.Tests.TestSupport;
using System.Data;

namespace Financial.Tests.Services
{
    public class DischargeRecordMapperTests
    {
        [Fact]
        public void ReadAll_Ipd_MapsThaiColumnsAndHandlesNulls()
        {
            var table = new DataTable();
            foreach (var c in new[] { "ลำดับที่", "รายชื่อ", "สิทธิ์การรักษา", "HN", "AN", "Admit", "D/C", "จำนวนเงิน" })
                table.Columns.Add(c, c == "ลำดับที่" ? typeof(int) : typeof(string));
            table.Rows.Add(1, "นาย ก", "UC", "650001", "AN001", "2026-10-01", "2026-10-05", "12500.50");
            table.Rows.Add(2, DBNull.Value, "SSS", "650002", "AN002", DBNull.Value, "2026-10-06", " 300 ");

            using var reader = table.CreateDataReader();
            var items = DischargeRecordMapper.ReadAll(reader, PatientTypeCodes.IPD);

            items.Should().HaveCount(2);
            items[0].Should().BeEquivalentTo(new { Id = 1, FullName = "นาย ก", TreatmentRight = "UC", HN = "650001", AN = "AN001", Admit = "2026-10-01", DCDate = "2026-10-05", Amount = "12500.50" });
            items[1].FullName.Should().BeEmpty();
            items[1].Admit.Should().BeEmpty();
            items[1].Amount.Should().Be("300");
        }

        [Fact]
        public void ReadAll_Opd_MapsVnClinicAndBillingCondition()
        {
            var table = new DataTable();
            foreach (var c in new[] { "ลำดับที่", "รายชื่อ", "สิทธิ์การรักษา", "เงื่อนไขการเรียกเก็บ", "HN", "VN", "วันที่", "จำนวนเงิน", "คลินิก" })
                table.Columns.Add(c);
            table.Rows.Add("7", "นาง ข", "CSMBS", "เบิกจ่ายตรง", "660010", "VN99", "2026-10-02", "850", "หัวใจ");

            using var reader = table.CreateDataReader();
            var item = DischargeRecordMapper.ReadAll(reader, PatientTypeCodes.OPD).Single();

            item.Id.Should().Be(7);
            item.AN.Should().Be("VN99");
            item.BillCon.Should().Be("เบิกจ่ายตรง");
            item.Clinic.Should().Be("หัวใจ");
            item.DCDate.Should().Be("2026-10-02");
        }

        [Fact]
        public void ReadAll_MissingColumn_ThrowsSoServiceCanReportError()
        {
            var table = new DataTable();
            table.Columns.Add("ลำดับที่");

            using var reader = table.CreateDataReader();
            var act = () => DischargeRecordMapper.ReadAll(reader, PatientTypeCodes.IPD);

            // SqlDataReader โยน IndexOutOfRangeException (DischargeService จับไว้แล้ว), DataTableReader โยน ArgumentException
            act.Should().Throw<Exception>().Which.Should().Match<Exception>(e => e is IndexOutOfRangeException || e is ArgumentException);
        }

        [Theory]
        [InlineData("1,234.50", 1234.50)]
        [InlineData("99", 99)]
        [InlineData("", 0)]
        [InlineData("abc", 0)]
        public void ParseAmount_HandlesFormats(string value, decimal expected)
        {
            DischargeQueryResult.ParseAmount(value).Should().Be(expected);
        }

        private static DischargeService CreateService(Dictionary<string, string?> settings) => new(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DischargeService>.Instance);

        [Fact]
        public void StoredProcedureFor_DefaultsToOpdAllPayor()
        {
            var service = CreateService([]);

            service.StoredProcedureFor(PatientTypeCodes.IPD).Should().Be("pGetQSHCPatientDischarge");
            service.StoredProcedureFor(PatientTypeCodes.OPD).Should().Be("pGetQSHCPatientDischargeOPDAllPayor", "OPD ใช้ SP ทุกสิทธิ์");
        }

        [Fact]
        public void StoredProcedureFor_IsConfigurable_ToSwitchBack()
        {
            var service = CreateService(new() { ["Discharges:OpdProcedure"] = "pGetQSHCPatientDischargeOPD" });

            service.StoredProcedureFor(PatientTypeCodes.OPD).Should().Be("pGetQSHCPatientDischargeOPD");
        }

        [Theory]
        [InlineData("pGetX; DROP TABLE y", "fallback")]
        [InlineData("exec pX", "fallback")]
        [InlineData("", "fallback")]
        [InlineData(null, "fallback")]
        [InlineData("dbo.pGetQSHCPatientDischargeOPDAllPayor", "dbo.pGetQSHCPatientDischargeOPDAllPayor")]
        public void ValidProcedureName_RejectsAnythingButPlainNames(string? configured, string expected)
        {
            DischargeService.ValidProcedureName(configured, "fallback").Should().Be(expected);
        }
    }

    public class DischargesControllerTests
    {
        private readonly Mock<IDischargeService> _service = new();
        private readonly TimeProvider _time = new FixedLocalTimeProvider(new DateTime(2026, 10, 22, 9, 0, 0));

        [Fact]
        public async Task IPD_NoDates_QueriesYesterday()
        {
            _service.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<DateRange>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string t, DateRange r, CancellationToken _) => new DischargeQueryResult(t, r, []));
            var controller = new DischargesController(_service.Object, _time);

            var result = await controller.IPD(null, null, null, CancellationToken.None);

            var model = result.Should().BeOfType<ViewResult>().Which.Model.Should().BeOfType<DischargeQueryResult>().Subject;
            model.Range.Start.Should().Be(new DateOnly(2026, 10, 21));
            _service.Verify(s => s.GetAsync(PatientTypeCodes.IPD, It.IsAny<DateRange>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task OPD_InvalidDates_ShowsErrorWithoutQuerying()
        {
            var controller = new DischargesController(_service.Object, _time);

            var result = await controller.OPD("2026-13-01", null, null, CancellationToken.None);

            var model = result.Should().BeOfType<ViewResult>().Which.Model.Should().BeOfType<DischargeQueryResult>().Subject;
            model.Succeeded.Should().BeFalse();
            _service.VerifyNoOtherCalls();
        }

        private static VmDischargesPD Item(int id, string right, string amount) => new()
        {
            Id = id, FullName = $"ผู้ป่วย {id}", TreatmentRight = right, BillCon = "", HN = $"HN{id}", AN = $"VN{id}",
            Admit = "", DCDate = "2026-10-21", Amount = amount, Clinic = "หัวใจ",
        };

        private void SetupItems(params VmDischargesPD[] items) =>
            _service.Setup(s => s.GetAsync(It.IsAny<string>(), It.IsAny<DateRange>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((string t, DateRange r, CancellationToken _) => new DischargeQueryResult(t, r, items));

        [Fact]
        public async Task OPD_FilterByRight_ShowsOnlyThatRight_AndListsAllRights()
        {
            SetupItems(Item(1, "บัตรทอง", "100"), Item(2, "ข้าราชการ", "500"), Item(3, " บัตรทอง ", "50"), Item(4, "", "10"));
            var controller = new DischargesController(_service.Object, _time);

            var result = await controller.OPD(null, null, "บัตรทอง", CancellationToken.None);

            var model = result.Should().BeOfType<ViewResult>().Which.Model.Should().BeOfType<DischargeQueryResult>().Subject;
            model.RightFilter.Should().Be("บัตรทอง");
            model.Items.Select(i => i.Id).Should().Equal(1, 3);
            model.TotalAmount.Should().Be(150m);
            model.UnfilteredCount.Should().Be(4);
            model.Rights.Select(r => r.Name).Should().Equal("ข้าราชการ", "บัตรทอง", DischargeQueryResult.NO_RIGHT); // เรียงตามยอดเงิน
            model.Rights.Single(r => r.Name == "บัตรทอง").Count.Should().Be(2);
        }

        [Fact]
        public async Task OPD_UnknownRight_ShowsAllRows()
        {
            SetupItems(Item(1, "บัตรทอง", "100"), Item(2, "ข้าราชการ", "500"));
            var controller = new DischargesController(_service.Object, _time);

            var model = (DischargeQueryResult)((ViewResult)await controller.OPD(null, null, "ไม่มีสิทธิ์นี้", CancellationToken.None)).Model!;

            model.RightFilter.Should().BeNull();
            model.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task Export_UsesSameRightFilter_AsScreen()
        {
            SetupItems(Item(1, "บัตรทอง", "100"), Item(2, "ข้าราชการ", "500"), Item(3, "บัตรทอง", "50"));
            var controller = new DischargesController(_service.Object, _time);

            var result = await controller.Export("O", "2026-10-01", "2026-10-21", "บัตรทอง", CancellationToken.None);

            var file = result.Should().BeOfType<FileContentResult>().Subject;
            file.FileDownloadName.Should().Be("DC_OPD_2026-10-01_2026-10-21_บัตรทอง.xlsx");

            using var package = new OfficeOpenXml.ExcelPackage(new MemoryStream(file.FileContents));
            var ws = package.Workbook.Worksheets[0];
            ws.Dimension.Rows.Should().Be(3, "หัวตาราง + 2 แถวของสิทธิ์บัตรทอง");
            ws.Cells[2, 3].Text.Should().Be("บัตรทอง");
            ws.Cells[3, 3].Text.Should().Be("บัตรทอง");
        }

        [Fact]
        public async Task Export_NoRight_ExportsAllRows()
        {
            SetupItems(Item(1, "บัตรทอง", "100"), Item(2, "ข้าราชการ", "500"));
            var controller = new DischargesController(_service.Object, _time);

            var file = (FileContentResult)await controller.Export("O", "2026-10-01", "2026-10-21", null, CancellationToken.None);

            file.FileDownloadName.Should().Be("DC_OPD_2026-10-01_2026-10-21.xlsx");
            using var package = new OfficeOpenXml.ExcelPackage(new MemoryStream(file.FileContents));
            package.Workbook.Worksheets[0].Dimension.Rows.Should().Be(3);
        }

        [Theory]
        [InlineData("สิทธิ/ข้าราชการ:พิเศษ", "DC_IPD_2026-10-01_2026-10-21_สิทธิ_ข้าราชการ_พิเศษ.xlsx")]
        public void ExportFileName_RemovesInvalidCharacters(string right, string expected)
        {
            var range = new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 21));
            DischargesController.ExportFileName(PatientTypeCodes.IPD, range, right).Should().Be(expected);
        }
    }
}
