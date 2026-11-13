using Financial.Controllers;
using Financial.Database.InterfaceSystem.Contexts;
using Financial.Database.InterfaceSystem.Models;
using Financial.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;

namespace Financial.Tests.Services
{
    public class EdcTests
    {
        private static PatientEdcregister Edc(string code, DateTime? post, int uid = 0)
            => new() { Uid = uid, Code = code, PostDate = post, TransactionDate = post ?? DateTime.Today, PersonalId = "0", AppCode = "A", StatusFlag = "A", Timestamp = [] };

        private static async Task<(InterfaceSystemController Controller, InterfaceSystemDbContext Db)> CreateAsync(params PatientEdcregister[] rows)
        {
            var db = new InterfaceSystemDbContext(new DbContextOptionsBuilder<InterfaceSystemDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
            db.PatientEdcregisters.AddRange(rows);
            await db.SaveChangesAsync();
            return (new InterfaceSystemController(db, Options.Create(new FileUploadOptions()), NullLogger<InterfaceSystemController>.Instance), db);
        }

        private static List<PatientEdcregister> Model(IActionResult result) => ((ViewResult)result).Model.Should().BeAssignableTo<List<PatientEdcregister>>().Subject;

        [Fact]
        public async Task EDCIndex_FiltersByPostDateRange_InclusiveOfEndDay_AndByCode()
        {
            var (controller, _) = await CreateAsync(
                Edc("AAA", new DateTime(2026, 11, 1, 0, 0, 0), 1),
                Edc("AAA", new DateTime(2026, 11, 10, 15, 30, 0), 2),
                Edc("BBB", new DateTime(2026, 11, 10, 9, 0, 0), 3),
                Edc("AAA", new DateTime(2026, 11, 11, 0, 0, 0), 4),
                Edc("AAA", null, 5));

            var range = Model(await controller.EDCIndex(new DateTime(2026, 11, 1), new DateTime(2026, 11, 10), null, default));
            range.Select(r => r.Uid).Should().Equal(2, 3, 1).And.NotContain(4, "ถึงสิ้นวันที่ 10 ไม่รวมวันที่ 11").And.NotContain(5, "PostDate ว่างไม่ตรงช่วง");

            var code = Model(await controller.EDCIndex(null, null, " AAA ", default));
            code.Select(r => r.Uid).Should().Equal([4, 2, 1, 5], "ตัดช่องว่างหน้า-หลัง Code และเรียง PostDate ล่าสุดก่อน");

            var both = Model(await controller.EDCIndex(new DateTime(2026, 11, 10), new DateTime(2026, 11, 10), "BBB", default));
            both.Should().ContainSingle().Which.Uid.Should().Be(3);

            controller.ViewData["Filtered"].Should().Be(true);
        }

        [Fact]
        public async Task EDCIndex_SwapsReversedRange_AndNoFilterShowsLatest50()
        {
            var rows = Enumerable.Range(1, 60).Select(i => Edc("AAA", new DateTime(2026, 11, 1).AddHours(i), i)).ToArray();
            var (controller, _) = await CreateAsync(rows);

            var swapped = Model(await controller.EDCIndex(new DateTime(2026, 11, 30), new DateTime(2026, 11, 1), null, default));
            swapped.Should().HaveCount(60, "ช่วงที่กลับด้านถูกสลับให้");

            var latest = Model(await controller.EDCIndex(null, null, null, default));
            latest.Should().HaveCount(InterfaceSystemController.EDC_LATEST_ROWS);
            latest[0].Uid.Should().Be(60);
            controller.ViewData["Filtered"].Should().Be(false);
            controller.ViewData["HasMore"].Should().Be(true);
        }

        [Fact]
        public async Task DownloadTemplate_ReturnsPipeDelimitedFile_MatchingFieldCount_WithoutHeader()
        {
            var (controller, _) = await CreateAsync();

            var file = controller.DownloadTemplate().Should().BeOfType<FileContentResult>().Subject;

            file.FileDownloadName.Should().Be(EdcTemplate.FILE_NAME).And.StartWith("HCG14584", "ชื่อไฟล์ต้องผ่านเงื่อนไขนำเข้า");
            Path.GetExtension(file.FileDownloadName).Should().Be(".txt");
            var lines = Encoding.UTF8.GetString(file.FileContents).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            lines.Should().ContainSingle("ไม่มีบรรทัดหัวตาราง เพราะทุกบรรทัดถูกนำเข้าเป็นข้อมูล");
            lines[0].Split('|').Length.Should().Be(EdcTemplate.FIELDS.Count);
        }
    }
}
