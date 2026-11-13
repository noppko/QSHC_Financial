using Financial.Data;
using Financial.Models;
using Financial.Models.Receivables;
using Financial.Services.Dashboard;
using Microsoft.EntityFrameworkCore;

namespace Financial.Services.Receivables
{
    public sealed record ClaimBatchCreate(string SchemeCode, string? Side, DateTime? BillFrom, DateTime? BillTo, string? Payor, string? Note);

    public sealed class ClaimBatchDetail
    {
        public required ArClaimBatch Batch { get; init; }
        public required IReadOnlyList<ArClaimBatchItem> Items { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        /// <summary>ลูกหนี้ในชุดที่มีผลการเรียกเก็บแล้ว (ยกเลิกชุดที่ส่งแล้วไม่ได้)</summary>
        public int WithResults { get; init; }
    }

    /// <summary>
    /// ชุดเรียกเก็บ: จัดชุดจากลูกหนี้ OPEN/APPEAL ของ 1 สิทธิ์ → ส่งออก Excel (template ผลการเรียกเก็บที่เติม VN/AN ไว้) → บันทึกส่ง (CLAIMED)
    /// ลูกหนี้ 1 รายการอยู่ในชุดที่ยังไม่ส่ง (DRAFT) ได้ทีละชุด
    /// </summary>
    public class ClaimBatchService
    {
        public const int MAX_ITEMS = 5_000;
        public const int PAGE_SIZE = 100;

        private readonly ApplicationDbContext _db;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<ClaimBatchService> _logger;

        public ClaimBatchService(ApplicationDbContext db, TimeProvider timeProvider, ILogger<ClaimBatchService> logger)
        {
            _db = db;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        /// <summary>ลูกหนี้ที่จัดชุดได้: สิทธิ์นี้, OPEN/APPEAL, ยังมีคงค้าง, ไม่อยู่ในชุดที่ยังไม่ส่ง</summary>
        public IQueryable<ArReceivable> Eligible(ClaimBatchCreate c)
        {
            var q = _db.ArReceivables.Where(r => r.SchemeCode == c.SchemeCode
                && (r.StatusCode == ArStatusCodes.OPEN || r.StatusCode == ArStatusCodes.APPEAL)
                && r.OutstandingAmount > 0
                && !_db.ArClaimBatchItems.Any(i => i.ReceivableId == r.Id && i.ClaimBatch!.IsActive && i.ClaimBatch.StatusCode == ArClaimBatchStatusCodes.DRAFT));
            if (c.Side is PatientTypeCodes.OPD or PatientTypeCodes.IPD) q = q.Where(r => r.PatientTypeCode == c.Side);
            if (c.BillFrom is { } from) q = q.Where(r => r.BillDate >= from.Date);
            if (c.BillTo is { } to) q = q.Where(r => r.BillDate < to.Date.AddDays(1));
            if (!string.IsNullOrWhiteSpace(c.Payor)) q = q.Where(r => r.PayorName != null && r.PayorName.Contains(c.Payor.Trim()));
            return q;
        }

        public async Task<ImportActionResult> CreateAsync(ClaimBatchCreate c, CancellationToken cancellationToken = default)
        {
            var receivables = await Eligible(c).OrderBy(r => r.BillDate).ThenBy(r => r.Id).Take(MAX_ITEMS + 1)
                .Select(r => new { r.Id, r.OutstandingAmount }).ToListAsync(cancellationToken);
            if (receivables.Count == 0) return new(false, "ไม่มีลูกหนี้ที่จัดชุดได้ตามเงื่อนไขนี้ (ต้องเป็นสถานะ ตั้งหนี้/อุทธรณ์ และยังไม่อยู่ในชุดที่ยังไม่ส่ง)");
            if (receivables.Count > MAX_ITEMS) return new(false, $"ลูกหนี้เกิน {MAX_ITEMS:N0} รายการ — จำกัดช่วงวันออกบิลหรือเลือก OPD/IPD");

            var now = ThaiClock.Now(_timeProvider);
            var prefix = $"{c.SchemeCode}-{c.Side ?? "A"}-{now.Year + 543}-{now.Month:00}-";
            var count = await _db.ArClaimBatches.IgnoreQueryFilters().CountAsync(b => b.BatchNo.StartsWith(prefix), cancellationToken);
            var batch = new ArClaimBatch
            {
                BatchNo = prefix + (count + 1).ToString("0000"),
                SchemeCode = c.SchemeCode,
                PatientTypeCode = c.Side,
                PayorFilter = Clip(c.Payor?.Trim(), 100),
                PeriodFrom = c.BillFrom?.Date,
                PeriodTo = c.BillTo?.Date,
                Note = Clip(c.Note?.Trim(), 500),
                ItemCount = receivables.Count,
                TotalAmount = receivables.Sum(r => r.OutstandingAmount),
                Items = receivables.Select(r => new ArClaimBatchItem { ReceivableId = r.Id, ClaimedAmount = r.OutstandingAmount }).ToList(),
            };
            _db.ArClaimBatches.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Claim batch {BatchNo} created: {Count} receivables, {Amount:N2}", batch.BatchNo, batch.ItemCount, batch.TotalAmount);
            return new(true, batch.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        public Task<List<ArClaimBatch>> GetBatchesAsync(string schemeCode, int take = 50, CancellationToken cancellationToken = default)
            => _db.ArClaimBatches.AsNoTracking().Where(b => b.SchemeCode == schemeCode).OrderByDescending(b => b.Id).Take(take).ToListAsync(cancellationToken);

        public async Task<ClaimBatchDetail?> GetDetailAsync(int id, int page, CancellationToken cancellationToken = default)
        {
            var batch = await _db.ArClaimBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
            if (batch is null) return null;
            page = Math.Max(1, page);
            var items = _db.ArClaimBatchItems.AsNoTracking().Where(i => i.ClaimBatchId == id);
            return new ClaimBatchDetail
            {
                Batch = batch,
                Items = await items.Include(i => i.Receivable).OrderBy(i => i.Receivable!.BillDate).ThenBy(i => i.Id)
                    .Skip((page - 1) * PAGE_SIZE).Take(PAGE_SIZE).ToListAsync(cancellationToken),
                Page = page,
                PageSize = PAGE_SIZE,
                WithResults = await items.CountAsync(i => i.Receivable!.PaidAmount > 0 || i.Receivable.DeniedAmount > 0, cancellationToken),
            };
        }

        /// <summary>แถวสำหรับ Excel ที่ส่งออกจากชุด — HN ใส่เฉพาะเมื่อ includeHn</summary>
        public async Task<(ArClaimBatch Batch, IReadOnlyList<ClaimTemplateRow> Rows)?> ExportRowsAsync(int id, bool includeHn, CancellationToken cancellationToken = default)
        {
            var batch = await _db.ArClaimBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
            if (batch is null) return null;
            var rows = await _db.ArClaimBatchItems.AsNoTracking().Where(i => i.ClaimBatchId == id)
                .OrderBy(i => i.Receivable!.BillDate).ThenBy(i => i.Id)
                .Select(i => new { i.Receivable!.VisitNumber, i.Receivable.Hn, i.Receivable.VisitStartDate, i.Receivable.DischargeDate, i.Receivable.BillNumber, i.ClaimedAmount })
                .ToListAsync(cancellationToken);
            return (batch, rows.Select(r => new ClaimTemplateRow(r.VisitNumber, includeHn ? r.Hn : null, r.VisitStartDate?.Date, r.DischargeDate?.Date, r.BillNumber, r.ClaimedAmount)).ToList());
        }

        public async Task<ImportActionResult> RemoveItemAsync(int batchId, int itemId, CancellationToken cancellationToken = default)
        {
            var batch = await _db.ArClaimBatches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
            if (batch?.StatusCode != ArClaimBatchStatusCodes.DRAFT) return new(false, "เอาออกได้เฉพาะชุดที่ยังไม่ส่ง");
            var item = await _db.ArClaimBatchItems.FirstOrDefaultAsync(i => i.Id == itemId && i.ClaimBatchId == batchId, cancellationToken);
            if (item is null) return new(false, "ไม่พบรายการ");
            item.IsActive = false;
            batch.ItemCount--;
            batch.TotalAmount -= item.ClaimedAmount;
            await _db.SaveChangesAsync(cancellationToken);
            return new(true, "เอาลูกหนี้ออกจากชุดแล้ว");
        }

        /// <summary>บันทึกส่งแล้ว: ชุด SENT, ลูกหนี้ OPEN/APPEAL → CLAIMED (ลงประวัติ)</summary>
        public async Task<ImportActionResult> MarkSentAsync(int batchId, DateTime? sentDate, string? referenceDocNo, string? userName, CancellationToken cancellationToken = default)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
                var batch = await _db.ArClaimBatches.Include(b => b.Items).ThenInclude(i => i.Receivable).FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
                if (batch is null) return new ImportActionResult(false, "ไม่พบชุดเรียกเก็บ");
                if (batch.StatusCode != ArClaimBatchStatusCodes.DRAFT) return new ImportActionResult(false, "ชุดนี้ส่งหรือยกเลิกไปแล้ว");
                if (!batch.Items.Any(i => i.IsActive)) return new ImportActionResult(false, "ชุดนี้ไม่มีลูกหนี้");

                var now = ThaiClock.Now(_timeProvider);
                batch.StatusCode = ArClaimBatchStatusCodes.SENT;
                batch.SentDate = (sentDate ?? now).Date;
                batch.ReferenceDocNo = Clip(referenceDocNo?.Trim(), 100);
                batch.SentBy = Clip(userName, 100);
                var changed = 0;
                foreach (var r in batch.Items.Where(i => i.IsActive && i.Receivable is not null).Select(i => i.Receivable!).Where(r => r.StatusCode is ArStatusCodes.OPEN or ArStatusCodes.APPEAL))
                {
                    r.StatusHistories.Add(new ArStatusHistory { FromStatus = r.StatusCode, ToStatus = ArStatusCodes.CLAIMED, Reason = Clip($"ส่งเรียกเก็บชุด {batch.BatchNo}{Doc(batch.ReferenceDocNo)}", 500), ChangedAt = now });
                    r.StatusCode = ArStatusCodes.CLAIMED;
                    changed++;
                }
                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                _logger.LogInformation("Claim batch {BatchNo} sent by {User}: {Count} receivables claimed", batch.BatchNo, userName, changed);
                return new ImportActionResult(true, $"บันทึกส่งชุด {batch.BatchNo} แล้ว — ลูกหนี้ {changed:N0} รายการเป็น \"ส่งเรียกเก็บแล้ว\"");
            });
        }

        /// <summary>ยกเลิกชุด: ยังไม่ส่ง → ยกเลิกเลย · ส่งแล้ว → ได้เฉพาะเมื่อยังไม่มีผลการเรียกเก็บ (ลูกหนี้ CLAIMED กลับเป็น OPEN)</summary>
        public async Task<ImportActionResult> CancelAsync(int batchId, string? userName, CancellationToken cancellationToken = default)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
                var batch = await _db.ArClaimBatches.Include(b => b.Items).ThenInclude(i => i.Receivable).FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
                if (batch is null) return new ImportActionResult(false, "ไม่พบชุดเรียกเก็บ");
                if (batch.StatusCode == ArClaimBatchStatusCodes.CANCELLED) return new ImportActionResult(false, "ชุดนี้ยกเลิกไปแล้ว");

                var now = ThaiClock.Now(_timeProvider);
                var reverted = 0;
                if (batch.StatusCode == ArClaimBatchStatusCodes.SENT)
                {
                    var receivables = batch.Items.Where(i => i.IsActive && i.Receivable is not null).Select(i => i.Receivable!).ToList();
                    if (receivables.Any(r => r.PaidAmount > 0 || r.DeniedAmount > 0))
                        return new ImportActionResult(false, "ยกเลิกไม่ได้ — มีลูกหนี้ในชุดที่บันทึกผลการเรียกเก็บแล้ว (ยกเลิกไฟล์นำเข้าที่เกี่ยวข้องก่อน)");

                    // ลูกหนี้ที่ยังอยู่ในชุดอื่นที่ส่งแล้ว คงเป็น CLAIMED
                    var stillClaimed = new HashSet<int>();
                    foreach (var chunk in receivables.Select(r => r.Id).Chunk(RemittanceImportService.KEY_CHUNK))
                    {
                        stillClaimed.UnionWith(await _db.ArClaimBatchItems.AsNoTracking()
                            .Where(i => i.ClaimBatchId != batch.Id && i.ClaimBatch!.StatusCode == ArClaimBatchStatusCodes.SENT && chunk.Contains(i.ReceivableId))
                            .Select(i => i.ReceivableId).ToListAsync(cancellationToken));
                    }
                    foreach (var r in receivables.Where(r => r.StatusCode == ArStatusCodes.CLAIMED && !stillClaimed.Contains(r.Id)))
                    {
                        r.StatusHistories.Add(new ArStatusHistory { FromStatus = r.StatusCode, ToStatus = ArStatusCodes.OPEN, Reason = Clip($"ยกเลิกชุดเรียกเก็บ {batch.BatchNo}", 500), ChangedAt = now });
                        r.StatusCode = ArStatusCodes.OPEN;
                        reverted++;
                    }
                }
                batch.StatusCode = ArClaimBatchStatusCodes.CANCELLED;
                batch.Note = Clip($"{batch.Note} [ยกเลิกโดย {userName} {now:dd/MM/yyyy HH:mm}]".Trim(), 500);
                foreach (var item in batch.Items) item.IsActive = false; // ลูกหนี้กลับไปจัดชุดใหม่ได้
                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                _logger.LogInformation("Claim batch {BatchNo} cancelled by {User}: {Count} receivables back to open", batch.BatchNo, userName, reverted);
                return new ImportActionResult(true, reverted > 0 ? $"ยกเลิกชุดแล้ว — ลูกหนี้ {reverted:N0} รายการกลับเป็น \"ตั้งหนี้\"" : "ยกเลิกชุดแล้ว");
            });
        }

        private static string Doc(string? referenceDocNo) => string.IsNullOrEmpty(referenceDocNo) ? "" : $" (หนังสือ {referenceDocNo})";

        private static string? Clip(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
    }
}
