using Financial.Data;
using Financial.Models;
using Financial.Models.Receivables;
using Financial.Services;
using Financial.Services.Receivables;
using Financial.Services.Reports;
using Financial.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Financial.Tests.Services
{
    /// <summary>ลูกหนี้สิทธิ ระยะ 1: ตั้งหนี้จาก HIS (upsert/ยกเลิก) และภาพรวม/รายการ — ข้อมูลสมมติทั้งหมด</summary>
    public class ReceivableTests
    {
        private static readonly DateTime TODAY = new(2026, 11, 13, 10, 0, 0);
        private static readonly DateTime FROM = new(2026, 8, 1);
        private static readonly DateTime TO = new(2026, 11, 14);

        private sealed class FakeHis : IReceivableHisSource
        {
            public List<HisCreditBill> Bills { get; set; } = [];

            public Task<IReadOnlyList<HisCreditBill>> LoadAsync(DateTime from, DateTime to, IProgress<int>? rowsRead = null, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<HisCreditBill>>(Bills.Where(b => b.BillDate >= from && b.BillDate < to).ToList());
        }

        private static HisCreditBill Bill(long uid, string cat = "A", decimal amount = 1000m, string? payorName = "สปสช", string? agreement = "ในเขต",
            string side = "I", string? enc = "INPAT", DateTime? billDate = null, string? vn = null)
            => new(uid, $"I69{uid:000000}", billDate ?? new DateTime(2026, 11, 1), side, enc, 9000 + uid, vn ?? $"AN{uid:0000000}", $"HN{uid:000000}",
                new DateTime(2026, 10, 25), new DateTime(2026, 10, 30), $"PD{cat}", payorName, cat, agreement, amount);

        private static ReceivableSyncService Sync(ApplicationDbContext db, FakeHis his)
            => new(db, his, Microsoft.Extensions.Options.Options.Create(new FiscalReportOptions()), NullLogger<ReceivableSyncService>.Instance);

        private static ReceivableQueryService Query(ApplicationDbContext db)
        {
            var names = new Mock<IReferenceValueService>();
            names.Setup(n => n.GetByCategoryAsync(It.IsAny<string>())).ReturnsAsync([]);
            return new ReceivableQueryService(db, Microsoft.Extensions.Options.Options.Create(new FiscalReportOptions()), names.Object,
                new FixedLocalTimeProvider(TODAY), NullLogger<ReceivableQueryService>.Instance);
        }

        [Fact]
        public async Task Sync_AddsCreditBills_ClassifiedIntoFourSchemes_LikeFiscalReport()
        {
            using var db = TestHost.CreateInMemoryDb();
            var his = new FakeHis
            {
                Bills =
                [
                    Bill(1, "A", 1000m, agreement: "ส่งต่อนอกเขต"),
                    Bill(2, "A", 500m, payorName: "สปสช(อปท)"),          // ชื่อมี "อปท" → LGO มาก่อน PYRACAT A
                    Bill(3, "B", 300m, side: "O", enc: "OUPAT"),
                    Bill(4, "C", 200m, side: "I", enc: "OUPAT"),         // บิล IPD แต่ visit เป็น VN → OPD
                    Bill(5, "G", 999m),                                 // บริษัทประกัน — ไม่อยู่ใน 4 สิทธิ์
                ],
            };

            var result = await Sync(db, his).SyncAsync(FROM, TO);

            result.Should().Be(new ReceivableSyncResult(5, 4, 0, 0, 1));
            var rows = await db.ArReceivables.AsNoTracking().OrderBy(r => r.HisBillUid).ToListAsync();
            rows.Select(r => r.SchemeCode).Should().Equal(PayorSchemeCodes.NHSO, PayorSchemeCodes.LGO, PayorSchemeCodes.CSMBS, PayorSchemeCodes.SSO);
            rows[0].IsOutOfZone.Should().BeTrue();
            rows[2].PatientTypeCode.Should().Be(PatientTypeCodes.OPD);
            rows[3].PatientTypeCode.Should().Be(PatientTypeCodes.OPD, "OPD/IPD ตามประเภท visit เหมือนรายงาน");
            rows.Should().OnlyContain(r => r.StatusCode == ArStatusCodes.OPEN && r.OutstandingAmount == r.BilledAmount && r.CreatedBy == "System");
        }

        [Fact]
        public async Task Sync_Again_UpdatesAmountWithHistory_CancelsMissing_AndReopensWhenBack()
        {
            using var db = TestHost.CreateInMemoryDb();
            var his = new FakeHis { Bills = [Bill(1, amount: 1000m), Bill(2, amount: 500m)] };
            await Sync(db, his).SyncAsync(FROM, TO);

            his.Bills = [Bill(1, amount: 1200m)]; // ยอดเปลี่ยน และบิล 2 หายไป
            var second = await Sync(db, his).SyncAsync(FROM, TO);

            second.Should().Be(new ReceivableSyncResult(1, 0, 1, 1, 0));
            var rows = await db.ArReceivables.AsNoTracking().Include(r => r.StatusHistories).OrderBy(r => r.HisBillUid).ToListAsync();
            rows[0].BilledAmount.Should().Be(1200m);
            rows[0].OutstandingAmount.Should().Be(1200m);
            rows[0].StatusHistories.Should().ContainSingle().Which.Reason.Should().Contain("1,000.00 → 1,200.00");
            rows[1].StatusCode.Should().Be(ArStatusCodes.CANCELLED);
            rows[1].StatusHistories.Should().ContainSingle(h => h.ToStatus == ArStatusCodes.CANCELLED && h.FromStatus == ArStatusCodes.OPEN);

            his.Bills = [Bill(1, amount: 1200m), Bill(2, amount: 500m)];
            var third = await Sync(db, his).SyncAsync(FROM, TO);
            third.Updated.Should().Be(1, "บิล 1 ไม่เปลี่ยน · บิล 2 กลับมา");
            (await db.ArReceivables.AsNoTracking().SingleAsync(r => r.HisBillUid == 2)).StatusCode.Should().Be(ArStatusCodes.OPEN);
        }

        [Fact]
        public async Task Sync_HisReturnsNothing_KeepsOpenReceivables()
        {
            using var db = TestHost.CreateInMemoryDb();
            var his = new FakeHis { Bills = [Bill(1), Bill(2)] };
            await Sync(db, his).SyncAsync(FROM, TO);

            his.Bills = [];
            var result = await Sync(db, his).SyncAsync(FROM, TO);

            result.CancelSkipped.Should().BeTrue();
            (await db.ArReceivables.AsNoTracking().ToListAsync()).Should().OnlyContain(r => r.StatusCode == ArStatusCodes.OPEN, "กันยกเลิกทั้งหมดเมื่อ HIS ผิดปกติ");
        }

        [Fact]
        public async Task Sync_OnlyTouchesBillsInsideWindow()
        {
            using var db = TestHost.CreateInMemoryDb();
            var his = new FakeHis { Bills = [Bill(1, billDate: new DateTime(2026, 1, 5)), Bill(2)] };
            await Sync(db, his).SyncAsync(new DateTime(2026, 1, 1), TO);

            his.Bills = [Bill(2)];
            await Sync(db, his).SyncAsync(FROM, TO); // ช่วงนี้ไม่รวมบิล 1

            (await db.ArReceivables.AsNoTracking().SingleAsync(r => r.HisBillUid == 1)).StatusCode.Should().Be(ArStatusCodes.OPEN);
        }

        [Fact]
        public void BuildSql_ReplacesPayorAndExcludedPayorPlaceholders()
        {
            var sql = ReceivableHisSource.BuildSql(categoryCount: 2, keywordCount: 1, excludedCount: 1);

            sql.Should().Contain("PCAT.ValueCode IN (@c0, @c1) OR PD.PayorName LIKE @k0 OR PA.Name LIKE @k0)");
            sql.Should().Contain("AND ISNULL(PD.Code, '') NOT IN (@x0)");
            sql.Should().Contain("IsPaymentComplete = 'N'").And.Contain("WITH (NOLOCK)");
            sql.Should().NotContain("/*").And.NotContainAny("INSERT", "UPDATE ", "DELETE", "EXEC");
            System.Text.RegularExpressions.Regex.IsMatch(ReceivableHisSource.BuildSql(1, 0, 0), @"^\s*AND ISNULL\(PD\.Code", System.Text.RegularExpressions.RegexOptions.Multiline)
                .Should().BeFalse("ไม่มีผู้จ่ายที่ตัดออก = ไม่มีเงื่อนไข NOT IN (หัวไฟล์เป็นแค่ comment)");
            new ReceivableOptions().EffectiveExcludedPayors.Should().Equal("PDQSHC001");
        }

        [Fact]
        public async Task Overview_CardsPerScheme_AgingBuckets_ExcludeCancelled()
        {
            using var db = TestHost.CreateInMemoryDb();
            var his = new FakeHis
            {
                Bills =
                [
                    Bill(1, amount: 100m, billDate: TODAY.AddDays(-10)),
                    Bill(2, amount: 200m, billDate: TODAY.AddDays(-45), agreement: "นอกเขต"),
                    Bill(3, amount: 400m, billDate: TODAY.AddDays(-200), side: "O", enc: "OUPAT"),
                    Bill(4, "B", 50m, billDate: TODAY.AddDays(-5)),
                ],
            };
            await Sync(db, his).SyncAsync(TODAY.AddDays(-400), TO);
            var cancelled = await db.ArReceivables.SingleAsync(r => r.HisBillUid == 4);
            cancelled.StatusCode = ArStatusCodes.CANCELLED;
            await db.SaveChangesAsync();

            var o = await Query(db).GetOverviewAsync(PayorSchemeCodes.NHSO);

            o.Schemes.Select(s => s.Code).Should().Equal(PayorSchemeCodes.NHSO, PayorSchemeCodes.CSMBS, PayorSchemeCodes.SSO, PayorSchemeCodes.LGO);
            o.Schemes[0].Should().BeEquivalentTo(new { Count = 3, Outstanding = 700m, Over90 = 400m });
            o.Schemes[1].Count.Should().Be(0, "ยกเลิกแล้วไม่นับคงค้าง");
            o.Aging.Select(a => a.Amount).Should().Equal(100m, 200m, 0m, 0m, 400m);
            o.ByPatientType.Select(t => t.Amount).Should().Equal(400m, 300m);
            o.HasZones.Should().BeTrue();
            o.ByZone.Select(z => z.Amount).Should().Equal(500m, 200m);
            (await Query(db).GetOverviewAsync(PayorSchemeCodes.CSMBS)).ByZone.Should().BeEmpty("เขตเฉพาะ สปสช.");
        }

        [Fact]
        public async Task List_MasksIdentityForNonAdmin_AndFilters()
        {
            using var db = TestHost.CreateInMemoryDb();
            var his = new FakeHis
            {
                Bills =
                [
                    Bill(1, billDate: TODAY.AddDays(-10), vn: "AN69000123"),
                    Bill(2, billDate: TODAY.AddDays(-100), side: "O", enc: "OUPAT"),
                    Bill(3, billDate: TODAY.AddDays(-100)),
                ],
            };
            await Sync(db, his).SyncAsync(TODAY.AddDays(-400), TO);
            var q = Query(db);

            var masked = await q.GetListAsync(new ReceivableListQuery(PayorSchemeCodes.NHSO), includeIdentity: false);
            masked.Total.Should().Be(3);
            masked.Items.Should().OnlyContain(i => i.Hn == null);
            masked.Items.Single(i => i.BillNumber.EndsWith("000001")).VisitNumber.Should().Be("••••••0123");

            var admin = await q.GetListAsync(new ReceivableListQuery(PayorSchemeCodes.NHSO), includeIdentity: true);
            admin.Items.Should().OnlyContain(i => i.Hn != null);

            (await q.GetListAsync(new ReceivableListQuery(PayorSchemeCodes.NHSO, Aging: "91-180"), false)).Total.Should().Be(2);
            (await q.GetListAsync(new ReceivableListQuery(PayorSchemeCodes.NHSO, Side: "O"), false)).Total.Should().Be(1);
            (await q.GetListAsync(new ReceivableListQuery(PayorSchemeCodes.NHSO, Search: "AN69000123"), false)).Total.Should().Be(1);
            (await q.GetListAsync(new ReceivableListQuery(PayorSchemeCodes.NHSO, Search: "0123"), false)).Total.Should().Be(0, "ไม่ค้น VN/AN บางส่วน");
            (await q.GetListAsync(new ReceivableListQuery(PayorSchemeCodes.CSMBS), false)).Total.Should().Be(0);

            var paged = await q.GetListAsync(new ReceivableListQuery(PayorSchemeCodes.NHSO, Page: 2), false, pageSize: 2);
            paged.Items.Should().ContainSingle();
            paged.TotalOutstanding.Should().Be(3000m);
        }

        [Theory]
        [InlineData("AN69000123", "••••••0123")]
        [InlineData("123", "•••")]
        [InlineData(null, null)]
        public void Mask_KeepsLastFour(string? value, string? expected) => ReceivableQueryService.Mask(value).Should().Be(expected);

        [Fact]
        public void Worker_WindowStartsAtMidnight_AndIncludesToday()
        {
            var (from, to) = ReceivableSyncWorker.Window(TODAY, 90);
            from.Should().Be(new DateTime(2026, 8, 15));
            to.Should().Be(new DateTime(2026, 11, 14));
        }

        [Fact]
        public void Queue_AcceptsOneJobAtATime()
        {
            var queue = new ReceivableSyncQueue();
            queue.Enqueue(90).Should().BeTrue();
            queue.Enqueue(30).Should().BeFalse();
            queue.SetStatus(new ReceivableSyncStatus(RefreshStatus.DONE, DateTime.Now));
            queue.Enqueue(30).Should().BeTrue();
        }
    }
}
