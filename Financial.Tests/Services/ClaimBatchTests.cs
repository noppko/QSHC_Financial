using Financial.Data;
using Financial.Models;
using Financial.Models.Receivables;
using Financial.Services.Receivables;
using Financial.Services.Reports;
using Financial.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using OfficeOpenXml;

namespace Financial.Tests.Services
{
    /// <summary>ลูกหนี้สิทธิ ระยะ 3: ชุดเรียกเก็บ + ธง "ต้องตรวจ" เมื่อบิลใน HIS เปลี่ยนหลังส่งเรียกเก็บ — ข้อมูลสมมติทั้งหมด</summary>
    public class ClaimBatchTests
    {
        private static readonly DateTime NOW = new(2026, 11, 13, 10, 0, 0);

        static ClaimBatchTests()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        private static async Task<ApplicationDbContext> SeedAsync()
        {
            var db = TestHost.CreateInMemoryDb();
            ArReceivable Rec(long uid, string scheme, string side, decimal billed, DateTime billDate, string status = ArStatusCodes.OPEN)
            {
                var r = new ArReceivable { HisBillUid = uid, BillNumber = $"B{uid}", HisVisitUid = uid, VisitNumber = $"AN{uid}", Hn = $"H{uid}", SchemeCode = scheme,
                    PatientTypeCode = side, BilledAmount = billed, BillDate = billDate, VisitStartDate = billDate.AddDays(-3), StatusCode = status, LastSyncedAt = NOW };
                r.Recalculate();
                return r;
            }
            db.ArReceivables.AddRange(
                Rec(1, "NHSO", "I", 1000m, new DateTime(2026, 10, 5)),
                Rec(2, "NHSO", "I", 2000m, new DateTime(2026, 10, 20)),
                Rec(3, "NHSO", "O", 300m, new DateTime(2026, 10, 21)),
                Rec(4, "NHSO", "I", 500m, new DateTime(2026, 10, 22), ArStatusCodes.CANCELLED),
                Rec(5, "CSMBS", "I", 700m, new DateTime(2026, 10, 22)));
            await db.SaveChangesAsync();
            return db;
        }

        private static ClaimBatchService Service(ApplicationDbContext db) => new(db, new FixedLocalTimeProvider(NOW), NullLogger<ClaimBatchService>.Instance);

        [Fact]
        public async Task Create_TakesOpenReceivablesOfSchemeAndFilters_NotInAnotherDraft()
        {
            using var db = await SeedAsync();
            var service = Service(db);

            var created = await service.CreateAsync(new ClaimBatchCreate("NHSO", "I", null, null, null, null));
            created.Success.Should().BeTrue(created.Message);
            var batch = await db.ArClaimBatches.Include(b => b.Items).SingleAsync();
            batch.BatchNo.Should().Be("NHSO-I-2569-11-0001");
            batch.Items.Select(i => i.ReceivableId).Should().BeEquivalentTo([1, 2], "IPD ของ NHSO ที่ยังไม่ยกเลิก");
            batch.TotalAmount.Should().Be(3000m);

            (await service.CreateAsync(new ClaimBatchCreate("NHSO", "I", null, null, null, null))).Success.Should().BeFalse("ลูกหนี้อยู่ในชุดที่ยังไม่ส่งแล้ว");
            var opd = await service.CreateAsync(new ClaimBatchCreate("NHSO", null, new DateTime(2026, 10, 21), new DateTime(2026, 10, 31), null, null));
            opd.Success.Should().BeTrue();
            (await db.ArClaimBatches.CountAsync()).Should().Be(2);
            (await db.ArClaimBatches.OrderBy(b => b.Id).LastAsync()).BatchNo.Should().Be("NHSO-A-2569-11-0001");
        }

        [Fact]
        public async Task Send_MarksClaimed_CancelRevertsToOpen_AndImportCancelKeepsClaimed()
        {
            using var db = await SeedAsync();
            var service = Service(db);
            var id = int.Parse((await service.CreateAsync(new ClaimBatchCreate("NHSO", "I", null, null, null, null))).Message);

            (await service.RemoveItemAsync(id, (await db.ArClaimBatchItems.FirstAsync(i => i.ReceivableId == 2)).Id)).Success.Should().BeTrue();
            (await service.MarkSentAsync(id, new DateTime(2026, 11, 12), "สธ 0001/2569", "cash1")).Success.Should().BeTrue();

            var recs = await db.ArReceivables.AsNoTracking().Include(r => r.StatusHistories).ToDictionaryAsync(r => r.HisBillUid);
            recs[1].StatusCode.Should().Be(ArStatusCodes.CLAIMED);
            recs[1].StatusHistories.Should().Contain(h => h.Reason!.Contains("NHSO-I-2569-11-0001"));
            recs[2].StatusCode.Should().Be(ArStatusCodes.OPEN, "เอาออกจากชุดก่อนส่ง");
            (await db.ArClaimBatches.SingleAsync()).Should().BeEquivalentTo(new { StatusCode = ArClaimBatchStatusCodes.SENT, ItemCount = 1, TotalAmount = 1000m, SentBy = "cash1" });
            (await service.RemoveItemAsync(id, 1)).Success.Should().BeFalse("ส่งแล้วแก้ไม่ได้");

            // นำเข้าผล แล้วยกเลิกไฟล์ → ลูกหนี้กลับเป็น CLAIMED (ยังอยู่ในชุดที่ส่งแล้ว) ไม่ใช่ OPEN
            var imports = new RemittanceImportService(db, Microsoft.Extensions.Options.Options.Create(new ReceivableOptions()), NullLogger<RemittanceImportService>.Instance);
            var export = await service.ExportRowsAsync(id, includeHn: false);
            var bytes = Filled(export!.Value.Batch.BatchNo, export.Value.Rows, received: 1000m);
            var importId = (await imports.CreateAsync("NHSO", "REP-9", null, "r.xlsx", new MemoryStream(bytes))).BatchId!.Value;
            (await db.ArImportBatches.FindAsync(importId))!.ClaimBatchId.Should().Be(id, "อ่านเลขชุดจาก template");
            await imports.ConfirmAsync(importId, "cash1");
            (await db.ArReceivables.AsNoTracking().SingleAsync(r => r.HisBillUid == 1)).StatusCode.Should().Be(ArStatusCodes.PAID);
            (await service.CancelAsync(id, "cash1")).Success.Should().BeFalse("มีผลการเรียกเก็บแล้ว ยกเลิกชุดไม่ได้");
            await imports.CancelAsync(importId, "cash1");
            (await db.ArReceivables.AsNoTracking().SingleAsync(r => r.HisBillUid == 1)).StatusCode.Should().Be(ArStatusCodes.CLAIMED);

            (await service.CancelAsync(id, "cash1")).Success.Should().BeTrue();
            (await db.ArReceivables.AsNoTracking().SingleAsync(r => r.HisBillUid == 1)).StatusCode.Should().Be(ArStatusCodes.OPEN);
            (await service.CreateAsync(new ClaimBatchCreate("NHSO", "I", null, null, null, null))).Success.Should().BeTrue("ยกเลิกชุดแล้วจัดชุดใหม่ได้");
        }

        /// <summary>Excel จากชุด: เติม VN/AN + ยอดเรียกเก็บไว้ · ไม่มี HN ถ้าไม่ใช่ Admin · กรอกยอดที่ได้รับแล้วนำเข้าได้</summary>
        private static byte[] Filled(string batchNo, IReadOnlyList<ClaimTemplateRow> rows, decimal received)
        {
            using var package = new ExcelPackage(new MemoryStream(RemittanceExcel.CreateTemplate("NHSO", "สปสช.", [], batchNo, rows)));
            var ws = package.Workbook.Worksheets[RemittanceExcel.SHEET_DATA];
            ws.Cells[2, RemittanceExcel.COLUMNS.ToList().IndexOf(RemittanceExcel.COL_VN) + 1].Text.Should().Be("AN1");
            ws.Cells[2, RemittanceExcel.COLUMNS.ToList().IndexOf(RemittanceExcel.COL_HN) + 1].Value.Should().BeNull("ไม่ใช่ Admin ไม่ใส่ HN");
            ws.Cells[2, RemittanceExcel.COLUMNS.ToList().IndexOf(RemittanceExcel.COL_CLAIMED) + 1].GetValue<decimal>().Should().Be(1000m);
            for (var r = 0; r < rows.Count; r++) ws.Cells[r + 2, RemittanceExcel.COLUMNS.ToList().IndexOf(RemittanceExcel.COL_RECEIVED) + 1].Value = received;
            return package.GetAsByteArray();
        }

        // ---------------- ธงต้องตรวจ (บิลแก้หลังจำหน่าย) ----------------

        private sealed class FakeHis(List<HisCreditBill> bills) : IReceivableHisSource
        {
            public List<HisCreditBill> Bills { get; set; } = bills;
            public Task<IReadOnlyList<HisCreditBill>> LoadAsync(DateTime from, DateTime to, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<HisCreditBill>>(Bills.Where(b => b.BillDate >= from && b.BillDate < to).ToList());
        }

        private static HisCreditBill Bill(long uid, decimal amount, DateTime? billDate = null)
            => new(uid, $"B{uid}", billDate ?? new DateTime(2026, 10, 5), "I", "INPAT", uid, $"AN{uid}", $"H{uid}", new DateTime(2026, 10, 1), new DateTime(2026, 10, 4),
                "PDA", "สปสช", "A", "ในเขต", amount);

        [Fact]
        public async Task PharmacyAddsDrugAfterDischarge_FlagsReview_OnlyWhenAlreadyClaimed()
        {
            using var db = TestHost.CreateInMemoryDb();
            var his = new FakeHis([Bill(1, 1000m), Bill(2, 2000m)]);
            var sync = new ReceivableSyncService(db, his, Microsoft.Extensions.Options.Options.Create(new FiscalReportOptions()), NullLogger<ReceivableSyncService>.Instance);
            var (from, to) = (new DateTime(2026, 10, 1), new DateTime(2026, 11, 14));
            await sync.SyncAsync(from, to);
            var claim = Service(db);
            var id = int.Parse((await claim.CreateAsync(new ClaimBatchCreate("NHSO", null, null, null, null, null) with { BillTo = new DateTime(2026, 10, 5) })).Message);
            await claim.RemoveItemAsync(id, (await db.ArClaimBatchItems.FirstAsync(i => i.Receivable!.HisBillUid == 2)).Id);
            await claim.MarkSentAsync(id, null, null, "cash1"); // บิล 1 ส่งแล้ว, บิล 2 ยัง OPEN

            // เภสัชคีย์ยาเพิ่ม: บิล 1 ยอดเพิ่ม (ส่งแล้ว → ต้องตรวจ), บิล 2 ยอดเพิ่ม (ยังไม่ส่ง → อัปเดตเฉยๆ)
            his.Bills = [Bill(1, 1250m), Bill(2, 2300m)];
            await sync.SyncAsync(from, to);
            var recs = await db.ArReceivables.AsNoTracking().ToDictionaryAsync(r => r.HisBillUid);
            recs[1].Should().BeEquivalentTo(new { BilledAmount = 1250m, StatusCode = ArStatusCodes.CLAIMED });
            recs[1].ReviewReason.Should().Contain("1,000.00 → 1,250.00").And.Contain("หลังส่งเรียกเก็บ");
            recs[2].ReviewReason.Should().BeNull();
            recs[2].BilledAmount.Should().Be(2300m);

            // ยกเลิกบิลเดิมแล้วออกบิลใหม่ (UID ใหม่): บิล 1 หาย (ส่งแล้ว → ไม่ยกเลิกเอง ติดธง), บิล 3 ใหม่ → ตั้งหนี้
            his.Bills = [Bill(2, 2300m), Bill(3, 1250m, new DateTime(2026, 11, 10))];
            var result = await sync.SyncAsync(from, to);
            result.Added.Should().Be(1);
            var after = await db.ArReceivables.AsNoTracking().ToDictionaryAsync(r => r.HisBillUid);
            after[1].StatusCode.Should().Be(ArStatusCodes.CLAIMED, "ส่งเรียกเก็บแล้ว ระบบไม่ยกเลิกเอง");
            after[1].ReviewReason.Should().StartWith(ReceivableSyncService.MISSING_IN_HIS);
            after[3].StatusCode.Should().Be(ArStatusCodes.OPEN);

            // บิลกลับมา → ล้างธง "ไม่พบ" เอง · เจ้าหน้าที่กดตรวจแล้ว → ล้างธง + ลงประวัติ
            his.Bills = [Bill(1, 1250m), Bill(2, 2300m), Bill(3, 1250m, new DateTime(2026, 11, 10))];
            await sync.SyncAsync(from, to);
            (await db.ArReceivables.AsNoTracking().SingleAsync(r => r.HisBillUid == 1)).ReviewReason.Should().BeNull();

            his.Bills = [Bill(1, 1300m), Bill(2, 2300m), Bill(3, 1250m, new DateTime(2026, 11, 10))];
            await sync.SyncAsync(from, to);
            var query = new ReceivableQueryService(db, Microsoft.Extensions.Options.Options.Create(new FiscalReportOptions()), Moq.Mock.Of<Financial.Services.IReferenceValueService>(),
                new FixedLocalTimeProvider(NOW), NullLogger<ReceivableQueryService>.Instance);
            var flagged = await query.GetListAsync(new ReceivableListQuery("NHSO", NeedsReview: true), false);
            flagged.Items.Should().ContainSingle().Which.ReviewReason.Should().Contain("1,250.00 → 1,300.00");
            (await query.ClearReviewAsync(flagged.Items[0].Id, "ส่งเรียกเก็บส่วนต่างแล้ว", "cash1")).Should().BeTrue();
            (await query.GetListAsync(new ReceivableListQuery("NHSO", NeedsReview: true), false)).Total.Should().Be(0);
            (await db.ArStatusHistories.AsNoTracking().AnyAsync(h => h.Reason!.Contains("ตรวจแล้วโดย cash1") && h.Reason.Contains("ส่วนต่าง"))).Should().BeTrue();
        }
    }
}
