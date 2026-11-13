using Financial.Data;
using Financial.Models;
using Financial.Models.Receivables;
using Financial.Services.Receivables;
using Financial.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeOpenXml;

namespace Financial.Tests.Services
{
    /// <summary>ลูกหนี้สิทธิ ระยะ 2: template/อ่าน Excel, จับคู่ 3 ขั้น, นำเข้า → ยืนยัน → ยกเลิก — ข้อมูลสมมติทั้งหมด</summary>
    public class RemittanceTests
    {
        private static readonly MatchSettings SETTINGS = new();

        static RemittanceTests()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        // ---------------- Excel ----------------

        /// <summary>เติมข้อมูลลง template ของระบบ (ตามชื่อหัวคอลัมน์) แล้วคืนไฟล์</summary>
        private static byte[] Fill(string scheme, params Dictionary<string, object?>[] rows)
        {
            using var package = new ExcelPackage(new MemoryStream(RemittanceExcel.CreateTemplate(scheme, scheme, [("C01", "ตัวอย่าง")])));
            var ws = package.Workbook.Worksheets[RemittanceExcel.SHEET_DATA];
            for (var r = 0; r < rows.Length; r++)
                foreach (var (col, value) in rows[r])
                    ws.Cells[r + 2, RemittanceExcel.COLUMNS.ToList().IndexOf(col) + 1].Value = value;
            return package.GetAsByteArray();
        }

        private static Dictionary<string, object?> R(string? vn = null, object? hn = null, object? date = null, object? received = null, object? denied = null,
            string? bill = null, string? reason = null, string? statement = null, object? claimed = null) => new()
        {
            [RemittanceExcel.COL_VN] = vn, [RemittanceExcel.COL_HN] = hn, [RemittanceExcel.COL_VISIT_DATE] = date, [RemittanceExcel.COL_RECEIVED] = received,
            [RemittanceExcel.COL_DENIED] = denied, [RemittanceExcel.COL_BILL] = bill, [RemittanceExcel.COL_REASON_CODE] = reason,
            [RemittanceExcel.COL_STATEMENT] = statement, [RemittanceExcel.COL_CLAIMED] = claimed,
        };

        [Fact]
        public void Template_HasDataSheetFirst_HiddenSchemeMeta_AndReasonDropdown()
        {
            using var package = new ExcelPackage(new MemoryStream(RemittanceExcel.CreateTemplate("CSMBS", "กรมบัญชีกลาง", [("C01", "ไม่มีเอกสาร")])));
            var book = package.Workbook;

            book.Worksheets[0].Name.Should().Be(RemittanceExcel.SHEET_DATA);
            Enumerable.Range(1, RemittanceExcel.COLUMNS.Count).Select(c => book.Worksheets[0].Cells[1, c].Text).Should().Equal(RemittanceExcel.COLUMNS);
            book.Worksheets[RemittanceExcel.SHEET_META].Hidden.Should().Be(eWorkSheetHidden.VeryHidden);
            book.Worksheets[RemittanceExcel.SHEET_META].Cells[1, 2].Text.Should().Be("CSMBS");
            book.Worksheets[RemittanceExcel.SHEET_CODES].Cells[2, 1].Text.Should().Be("C01");
            book.Worksheets[0].DataValidations.Should().ContainSingle();
            book.Worksheets[0].Cells[2, 1, 3, RemittanceExcel.COLUMNS.Count].All(c => c.Value is null).Should().BeTrue("template ไม่มีข้อมูลผู้ป่วย");
            RemittanceExcel.FileName("csmbs").Should().Be("AR_REMITTANCE_CSMBS.xlsx");
        }

        [Fact]
        public void Parse_ReadsRows_ConvertsBuddhistYear_NumericHn_AndFlagsInvalid()
        {
            var file = Fill("NHSO",
                R(vn: "AN69000001", received: 25000d, date: "01/10/2569", statement: "REP-1"),
                R(hn: 1234567d, date: new DateTime(2026, 10, 3), received: "1,800.50"),
                R(hn: "00000003", received: 10d),                  // ไม่มีทั้ง VN และวันที่
                R(vn: "AN69000004", received: "abc"),              // ยอดไม่ใช่ตัวเลข
                R(),                                               // แถวว่าง ข้าม
                R(vn: "AN69000006", received: 0d, denied: 500d, reason: "C01"));

            var result = RemittanceExcel.Parse(new MemoryStream(file), 100);

            result.Errors.Should().BeEmpty();
            result.SchemeInFile.Should().Be("NHSO");
            result.Rows.Should().HaveCount(5);
            result.Rows[0].Should().Match<RemittanceRow>(r => r.RowNo == 2 && r.VisitDate == new DateTime(2026, 10, 1) && r.ReceivedAmount == 25000m && r.StatementNo == "REP-1" && r.Error == null);
            result.Rows[1].Hn.Should().Be("1234567", "ตัวเลขที่ Excel แปลงจาก HN กลับเป็นข้อความ");
            result.Rows[1].ReceivedAmount.Should().Be(1800.50m);
            result.Rows[2].Error.Should().Contain("VN/AN");
            result.Rows[3].Error.Should().Contain("ไม่ใช่ตัวเลข");
            result.Rows[4].Should().Match<RemittanceRow>(r => r.RowNo == 7 && r.DeniedAmount == 500m && r.ReasonCode == "C01");
        }

        [Fact]
        public void Parse_MissingRequiredHeader_OrTooManyRows_IsFileError()
        {
            using (var package = new ExcelPackage())
            {
                var ws = package.Workbook.Worksheets.Add("Sheet1");
                ws.Cells[1, 1].Value = "VN/AN";
                ws.Cells[2, 1].Value = "AN1";
                RemittanceExcel.Parse(new MemoryStream(package.GetAsByteArray()), 100).Errors.Should().Contain(e => e.Contains(RemittanceExcel.COL_RECEIVED));
            }

            var many = Fill("NHSO", R(vn: "A1", received: 1d), R(vn: "A2", received: 1d), R(vn: "A3", received: 1d));
            RemittanceExcel.Parse(new MemoryStream(many), 2).Errors.Should().Contain(e => e.Contains("เกิน 2"));
        }

        // ---------------- Matcher ----------------

        private static MatchCandidate C(int id, string scheme = "NHSO", string? vn = null, string? hn = "HN1", DateTime? start = null, decimal billed = 1000m,
            decimal? outstanding = null, string bill = "", string status = ArStatusCodes.OPEN, DateTime? discharge = null)
            => new(id, scheme, vn, hn, start ?? new DateTime(2026, 10, 1), discharge, bill == "" ? $"B{id}" : bill, billed, outstanding ?? billed, status);

        private static RemittanceRow Row(string? vn = null, string? hn = null, DateTime? date = null, decimal received = 1000m, decimal denied = 0m,
            string? bill = null, decimal? claimed = null, string? reason = null)
            => new(2, vn, hn, date, null, bill, claimed, received, denied, reason, null, null, null, null);

        [Fact]
        public void Match_Step1_ByVn_NarrowedByBill_OrMultiple()
        {
            var candidates = new[] { C(1, vn: "AN1"), C(2, vn: "AN2", bill: "I1"), C(3, vn: "AN2", bill: "I2"), C(4, "CSMBS", vn: "AN9") };

            RemittanceMatcher.Match(Row("AN1"), "NHSO", candidates, SETTINGS).Should().BeEquivalentTo(new { Result = ArMatchResultCodes.MATCHED, Step = "1", ReceivableId = (int?)1 });
            RemittanceMatcher.Match(Row("AN2", bill: "I2"), "NHSO", candidates, SETTINGS).ReceivableId.Should().Be(3);
            var multiple = RemittanceMatcher.Match(Row("AN2"), "NHSO", candidates, SETTINGS);
            multiple.Result.Should().Be(ArMatchResultCodes.MULTIPLE);
            multiple.CandidateIds.Should().Equal(2, 3);
            var other = RemittanceMatcher.Match(Row("AN9"), "NHSO", candidates, SETTINGS);
            other.Result.Should().Be(ArMatchResultCodes.NOT_FOUND);
            other.Message.Should().Contain("CSMBS", "ไม่จับคู่ข้ามสิทธิ์ แต่บอกว่าเป็นของสิทธิ์ไหน");
        }

        [Fact]
        public void Match_Step2_HnAndVisitOrDischargeDate_Step3_Suggested()
        {
            var day = new DateTime(2026, 10, 1);
            var candidates = new[]
            {
                C(1, hn: "H1", start: day),
                C(2, hn: "H2", start: day.AddDays(-5), discharge: day.AddDays(2)),
                C(3, hn: "H3", start: day.AddDays(1), billed: 5000m),
                C(4, hn: "H4", start: day, status: ArStatusCodes.CANCELLED),
            };

            RemittanceMatcher.Match(Row(hn: "H1", date: day), "NHSO", candidates, SETTINGS).Should().BeEquivalentTo(new { Result = ArMatchResultCodes.MATCHED, Step = "2", ReceivableId = (int?)1 });
            RemittanceMatcher.Match(Row(hn: "H2", date: day.AddDays(2)), "NHSO", candidates, SETTINGS).ReceivableId.Should().Be(2, "IPD ใช้วันจำหน่ายได้");
            var suggested = RemittanceMatcher.Match(Row(hn: "H3", date: day, received: 5000m), "NHSO", candidates, SETTINGS);
            suggested.Should().BeEquivalentTo(new { Result = ArMatchResultCodes.SUGGESTED, Step = "3", ReceivableId = (int?)3 });
            RemittanceMatcher.Match(Row(hn: "H3", date: day, received: 4000m), "NHSO", candidates, SETTINGS).Result.Should().Be(ArMatchResultCodes.NOT_FOUND, "ยอดต่างเกิน 1%");
            RemittanceMatcher.Match(Row(hn: "H4", date: day), "NHSO", candidates, SETTINGS).Result.Should().Be(ArMatchResultCodes.NOT_FOUND, "ลูกหนี้ที่ยกเลิกไม่จับคู่");
        }

        [Fact]
        public void Match_Single_DeniedAmountDiffAndInvalid()
        {
            var candidates = new[] { C(1, vn: "AN1", outstanding: 1000m) };

            RemittanceMatcher.Match(Row("AN1", received: 0m, denied: 1000m, reason: "C01"), "NHSO", candidates, SETTINGS).Result.Should().Be(ArMatchResultCodes.DENIED);
            var diff = RemittanceMatcher.Match(Row("AN1", received: 800m), "NHSO", candidates, SETTINGS);
            diff.Result.Should().Be(ArMatchResultCodes.AMOUNT_DIFF);
            diff.Message.Should().Contain("ขาด 200.00");
            RemittanceMatcher.Match(Row("AN1", received: 999.50m), "NHSO", candidates, SETTINGS).Result.Should().Be(ArMatchResultCodes.MATCHED, "ต่างไม่เกินเกณฑ์ปัดเศษ 1 บาท");
            RemittanceMatcher.Match(Row("AN1") with { Error = "x" }, "NHSO", candidates, SETTINGS).Result.Should().Be(ArMatchResultCodes.INVALID);
        }

        [Theory]
        [InlineData(100, new double[] { 1, 1, 1 }, new double[] { 33.33, 33.33, 33.34 })]
        [InlineData(900, new double[] { 600, 300 }, new double[] { 600, 300 })]
        [InlineData(10, new double[] { 0, 0 }, new double[] { 5, 5 })]
        public void Allocate_ProportionalWithRemainderOnLast(double amount, double[] weights, double[] expected)
            => RemittanceMatcher.Allocate((decimal)amount, weights.Select(w => (decimal)w).ToList()).Should().Equal(expected.Select(e => (decimal)e));

        // ---------------- Import flow ----------------

        private static async Task<ApplicationDbContext> SeedAsync()
        {
            var db = TestHost.CreateInMemoryDb();
            ArReceivable Rec(long uid, string scheme, string vn, string hn, decimal billed, DateTime start)
            {
                var r = new ArReceivable { HisBillUid = uid, BillNumber = $"I{uid}", HisVisitUid = uid, VisitNumber = vn, Hn = hn, SchemeCode = scheme, BilledAmount = billed,
                    BillDate = new DateTime(2026, 10, 10), VisitStartDate = start, StatusCode = ArStatusCodes.OPEN, LastSyncedAt = DateTime.Now };
                r.Recalculate();
                return r;
            }
            db.ArReceivables.AddRange(
                Rec(1, "NHSO", "AN1", "H1", 1000m, new DateTime(2026, 10, 1)),
                Rec(2, "NHSO", "AN2", "H2", 2000m, new DateTime(2026, 10, 2)),
                Rec(3, "NHSO", "AN3", "H3", 600m, new DateTime(2026, 10, 3)),
                Rec(4, "NHSO", "AN3", "H3", 300m, new DateTime(2026, 10, 3)),
                Rec(5, "CSMBS", "AN5", "H5", 700m, new DateTime(2026, 10, 5)));
            await db.SaveChangesAsync();
            return db;
        }

        private static RemittanceImportService Service(ApplicationDbContext db)
            => new(db, Microsoft.Extensions.Options.Options.Create(new ReceivableOptions()), NullLogger<RemittanceImportService>.Instance);

        private static byte[] NhsoFile(string statement = "REP-1") => Fill("NHSO",
            R(vn: "AN1", received: 1000d),                       // ครบ
            R(vn: "AN2", received: 1500d),                       // บางส่วน
            R(vn: "AN3", received: 900d),                        // 2 บิล → กระจาย 600/300
            R(vn: "AN5", received: 700d),                        // ของ CSMBS
            R(hn: "HX", date: "05/10/2026", received: 10d),      // ไม่พบ
            R(vn: "AN1", received: 1d, statement: statement));   // ซ้ำในไฟล์

        [Fact]
        public async Task Import_MatchesRows_RejectsDuplicateFile_AndWrongScheme()
        {
            using var db = await SeedAsync();
            var service = Service(db);

            var bytes = NhsoFile();
            var created = await service.CreateAsync("NHSO", "REP-1", new DateTime(2026, 11, 10), "rep1.xlsx", new MemoryStream(bytes));

            created.Success.Should().BeTrue(string.Join(",", created.Errors));
            var rows = await db.ArImportRows.AsNoTracking().OrderBy(r => r.RowNo).ToListAsync();
            rows.Select(r => r.MatchResultCode).Should().Equal(
                ArMatchResultCodes.MATCHED, ArMatchResultCodes.AMOUNT_DIFF, ArMatchResultCodes.MULTIPLE, ArMatchResultCodes.NOT_FOUND, ArMatchResultCodes.NOT_FOUND, ArMatchResultCodes.DUPLICATE);
            rows.Select(r => r.IsAccepted).Should().Equal(true, true, false, false, false, false);

            (await service.CreateAsync("NHSO", null, null, "again.xlsx", new MemoryStream(bytes))).Errors.Should().ContainSingle().Which.Should().Contain("เคยนำเข้าแล้ว");
            (await service.CreateAsync("CSMBS", null, null, "x.xlsx", new MemoryStream(NhsoFile("other")))).Errors.Should().ContainSingle().Which.Should().Contain("template ของสิทธิ์ NHSO");
        }

        [Fact]
        public async Task Confirm_RecordsPayments_UpdatesStatus_AndCancelReverses()
        {
            using var db = await SeedAsync();
            var service = Service(db);
            var batchId = (await service.CreateAsync("NHSO", "REP-1", new DateTime(2026, 11, 10), "rep1.xlsx", new MemoryStream(NhsoFile()))).BatchId!.Value;
            var rows = await db.ArImportRows.AsNoTracking().OrderBy(r => r.RowNo).ToListAsync();

            (await service.SetAcceptedAsync(rows[2].Id, true)).Success.Should().BeTrue("ยอมรับการกระจายยอด");
            (await service.SetAcceptedAsync(rows[4].Id, true)).Success.Should().BeFalse("แถวไม่พบยังรวมไม่ได้");
            (await service.ManualMatchAsync(rows[4].Id, "I5", "u")).Success.Should().BeFalse("ผูกข้ามสิทธิ์ไม่ได้");

            var confirm = await service.ConfirmAsync(batchId, "cash1");
            confirm.Success.Should().BeTrue(confirm.Message);

            var recs = await db.ArReceivables.AsNoTracking().Include(r => r.StatusHistories).OrderBy(r => r.Id).ToDictionaryAsync(r => r.HisBillUid);
            recs[1].Should().BeEquivalentTo(new { PaidAmount = 1000m, OutstandingAmount = 0m, StatusCode = ArStatusCodes.PAID });
            recs[2].Should().BeEquivalentTo(new { PaidAmount = 1500m, OutstandingAmount = 500m, StatusCode = ArStatusCodes.PARTIAL });
            recs[3].PaidAmount.Should().Be(600m);
            recs[4].PaidAmount.Should().Be(300m, "กระจาย 900 ตามสัดส่วนคงค้าง 600:300");
            recs[5].StatusCode.Should().Be(ArStatusCodes.OPEN, "ของสิทธิ์อื่นไม่ถูกแตะ");
            recs[1].StatusHistories.Should().Contain(h => h.ToStatus == ArStatusCodes.PAID && h.Reason!.Contains($"#{batchId}"));
            (await db.ArPayments.CountAsync()).Should().Be(4);
            (await db.ArImportBatches.SingleAsync()).Should().BeEquivalentTo(new { StatusCode = ArImportStatusCodes.CONFIRMED, ConfirmedBy = "cash1" });
            (await service.ConfirmAsync(batchId, "cash1")).Success.Should().BeFalse("ยืนยันซ้ำไม่ได้");

            // ไฟล์ใหม่ statement เดิม → ซ้ำ
            var again = Fill("NHSO", R(vn: "AN2", received: 500d, statement: "REP-1"), R(vn: "AN2", received: 500d, statement: "REP-2"));
            var secondId = (await service.CreateAsync("NHSO", null, null, "rep2.xlsx", new MemoryStream(again))).BatchId!.Value;
            (await db.ArImportRows.AsNoTracking().Where(r => r.ImportBatchId == secondId).OrderBy(r => r.RowNo).Select(r => r.MatchResultCode).ToListAsync())
                .Should().Equal(ArMatchResultCodes.DUPLICATE, ArMatchResultCodes.MATCHED);

            // ยกเลิกไฟล์แรก → ย้อนรับชำระ
            (await service.CancelAsync(batchId, "admin1")).Success.Should().BeTrue();
            var after = await db.ArReceivables.AsNoTracking().ToDictionaryAsync(r => r.HisBillUid);
            after[1].Should().BeEquivalentTo(new { PaidAmount = 0m, OutstandingAmount = 1000m, StatusCode = ArStatusCodes.OPEN });
            after[2].StatusCode.Should().Be(ArStatusCodes.OPEN);
            (await db.ArPayments.CountAsync()).Should().Be(0, "รับชำระของไฟล์ที่ยกเลิกเป็น inactive");
            (await db.ArImportBatches.FindAsync(batchId))!.CancelledBy.Should().Be("admin1");
        }

        [Fact]
        public async Task Denied_KeepsOutstanding_AndManualMatchWorks()
        {
            using var db = await SeedAsync();
            var service = Service(db);
            var file = Fill("NHSO", R(vn: "AN1", received: 0d, denied: 1000d, reason: "C01"), R(hn: "HX", date: "02/10/2026", received: 2000d));
            var id = (await service.CreateAsync("NHSO", "S1", null, "d.xlsx", new MemoryStream(file))).BatchId!.Value;
            var rows = await db.ArImportRows.AsNoTracking().OrderBy(r => r.RowNo).ToListAsync();

            (await service.ManualMatchAsync(rows[1].Id, "I2", "cash1")).Success.Should().BeTrue();
            (await db.ArImportRows.AsNoTracking().SingleAsync(r => r.Id == rows[1].Id)).Should().BeEquivalentTo(new { MatchResultCode = ArMatchResultCodes.MATCHED, MatchStep = "MANUAL", ReceivableId = (int?)2, IsAccepted = true });

            await service.ConfirmAsync(id, "cash1");
            var recs = await db.ArReceivables.AsNoTracking().ToDictionaryAsync(r => r.HisBillUid);
            recs[1].Should().BeEquivalentTo(new { DeniedAmount = 1000m, OutstandingAmount = 1000m, StatusCode = ArStatusCodes.DENIED }, "ยอดปฏิเสธยังเป็นหนี้ต้องติดตาม");
            recs[2].StatusCode.Should().Be(ArStatusCodes.PAID);
        }

        [Fact]
        public void DeriveStatus_KeepsCancelledAndWriteOff()
        {
            var r = new ArReceivable { BilledAmount = 100m, PaidAmount = 99.5m, StatusCode = ArStatusCodes.OPEN };
            r.Recalculate();
            r.DeriveStatus(1m, true).Should().Be(ArStatusCodes.PAID);
            r.DeriveStatus(1m, false).Should().Be(ArStatusCodes.OPEN);
            new ArReceivable { StatusCode = ArStatusCodes.CANCELLED }.DeriveStatus(1m, true).Should().Be(ArStatusCodes.CANCELLED);
        }
    }
}
