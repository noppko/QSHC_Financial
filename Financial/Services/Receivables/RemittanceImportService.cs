using Financial.Data;
using Financial.Models;
using Financial.Models.Receivables;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;

namespace Financial.Services.Receivables
{
    public sealed record ImportCreateResult(int? BatchId, IReadOnlyList<string> Errors)
    {
        public bool Success => BatchId is not null;
    }

    public sealed record ImportSummaryRow(string ResultCode, int Count, decimal Received);

    public sealed class ImportReview
    {
        public required ArImportBatch Batch { get; init; }
        public required IReadOnlyList<ImportSummaryRow> Summary { get; init; }
        public required IReadOnlyList<ArImportRow> Rows { get; init; }
        public int FilteredCount { get; init; }
        public int Page { get; init; }
        public int PageSize { get; init; }
        public decimal AcceptedReceived { get; init; }
        public int AcceptedCount { get; init; }
    }

    public sealed record ImportActionResult(bool Success, string Message);

    /// <summary>
    /// นำเข้าไฟล์ผลการเรียกเก็บ → จับคู่ → (ตรวจ/แก้คู่) → ยืนยันเป็นรับชำระ / ยกเลิก (ย้อนรับชำระของไฟล์)
    /// ยอดลูกหนี้คำนวณใหม่จาก ArPayments ที่ยัง active ทุกครั้ง (ไม่บวกลบสะสม)
    /// </summary>
    public class RemittanceImportService
    {
        public const int REVIEW_PAGE_SIZE = 200;
        internal const int KEY_CHUNK = 500; // จำนวนค่าต่อ IN (...) — ไม่ให้เกินขีดจำกัดพารามิเตอร์ของ SQL Server

        private readonly ApplicationDbContext _db;
        private readonly ReceivableOptions _options;
        private readonly ILogger<RemittanceImportService> _logger;

        public RemittanceImportService(ApplicationDbContext db, IOptions<ReceivableOptions> options, ILogger<RemittanceImportService> logger)
        {
            _db = db;
            _options = options.Value;
            _logger = logger;
        }

        private MatchSettings Settings => MatchSettings.From(_options);

        public async Task<ImportCreateResult> CreateAsync(string schemeCode, string? statementNo, DateTime? transferDate, string fileName, Stream content,
            CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            buffer.Position = 0;
            var sha = Convert.ToHexString(SHA256.HashData(buffer)).ToLowerInvariant();

            var previous = await _db.ArImportBatches.AsNoTracking()
                .Where(b => b.FileSha256 == sha && b.StatusCode != ArImportStatusCodes.CANCELLED)
                .Select(b => new { b.Id, b.CreatedAt }).FirstOrDefaultAsync(cancellationToken);
            if (previous is not null) return new(null, [$"ไฟล์นี้เคยนำเข้าแล้ว (ไฟล์ #{previous.Id} เมื่อ {previous.CreatedAt:dd/MM/yyyy HH:mm}) — ยกเลิกไฟล์เดิมก่อนถ้าต้องการนำเข้าใหม่"]);

            buffer.Position = 0;
            RemittanceParseResult parsed;
            try
            {
                parsed = RemittanceExcel.Parse(buffer, _options.MaxImportRows);
            }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException or ArgumentException)
            {
                _logger.LogWarning(ex, "Cannot read remittance file");
                return new(null, ["อ่านไฟล์ไม่ได้ — ต้องเป็น Excel (.xlsx) จาก template ของระบบ"]);
            }
            if (parsed.SchemeInFile is not null && !string.Equals(parsed.SchemeInFile, schemeCode, StringComparison.OrdinalIgnoreCase))
                return new(null, [$"ไฟล์นี้เป็น template ของสิทธิ์ {parsed.SchemeInFile} แต่เลือกนำเข้าเป็นสิทธิ์ {schemeCode}"]);
            if (parsed.Errors.Count > 0) return new(null, parsed.Errors);

            int? claimBatchId = null;
            if (!string.IsNullOrEmpty(parsed.ClaimBatchNo))
            {
                claimBatchId = await _db.ArClaimBatches.AsNoTracking()
                    .Where(c => c.BatchNo == parsed.ClaimBatchNo && c.SchemeCode == schemeCode).Select(c => (int?)c.Id).FirstOrDefaultAsync(cancellationToken);
            }

            var batch = new ArImportBatch
            {
                ClaimBatchId = claimBatchId,
                SchemeCode = schemeCode,
                StatementNo = Clip(statementNo?.Trim(), 100),
                TransferDate = transferDate?.Date,
                FileName = Clip(Path.GetFileName(fileName), 260) ?? "file.xlsx",
                FileSha256 = sha,
                RowCount = parsed.Rows.Count,
                TotalReceived = parsed.Rows.Sum(r => r.ReceivedAmount),
                StatusCode = ArImportStatusCodes.VALIDATED,
            };

            var candidates = await LoadCandidatesAsync(schemeCode, parsed.Rows, cancellationToken);
            var outcomes = parsed.Rows.Select(r => (Row: r, Outcome: RemittanceMatcher.Match(r, schemeCode, candidates, Settings))).ToList();
            var duplicates = await PaidStatementsAsync(outcomes.SelectMany(o => Targets(o.Outcome)).Distinct().ToList(), cancellationToken);

            var usedInFile = new Dictionary<int, int>(); // receivableId → RowNo แรกที่ใช้
            foreach (var (row, outcome) in outcomes)
            {
                var entity = ToEntity(row, outcome);
                var statement = row.StatementNo ?? batch.StatementNo;
                var targets = Targets(outcome);
                if (targets.Count > 0 && !string.IsNullOrEmpty(statement) && targets.Any(t => duplicates.Contains((t, statement))))
                {
                    SetResult(entity, ArMatchResultCodes.DUPLICATE, $"บันทึกผลของ statement {statement} ให้ลูกหนี้นี้ไปแล้ว");
                }
                else if (targets.Count > 0 && targets.FirstOrDefault(usedInFile.ContainsKey) is var used && used != 0)
                {
                    SetResult(entity, ArMatchResultCodes.DUPLICATE, $"ลูกหนี้เดียวกับแถว {usedInFile[used]} ในไฟล์นี้");
                }
                else
                {
                    foreach (var t in targets) usedInFile.TryAdd(t, row.RowNo);
                }
                batch.Rows.Add(entity);
            }

            _db.ArImportBatches.Add(batch);
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Remittance import #{Id} {Scheme}: {Rows} rows, {Matched} auto-accepted", batch.Id, schemeCode, batch.RowCount, batch.Rows.Count(r => r.IsAccepted));
            return new(batch.Id, []);
        }

        public Task<List<ArImportBatch>> GetBatchesAsync(string schemeCode, int take = 50, CancellationToken cancellationToken = default)
            => _db.ArImportBatches.AsNoTracking().Where(b => b.SchemeCode == schemeCode).OrderByDescending(b => b.Id).Take(take).ToListAsync(cancellationToken);

        public async Task<ImportReview?> GetReviewAsync(int batchId, string? result, int page, CancellationToken cancellationToken = default)
        {
            var batch = await _db.ArImportBatches.AsNoTracking().FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
            if (batch is null) return null;

            var rows = _db.ArImportRows.AsNoTracking().Where(r => r.ImportBatchId == batchId);
            var summary = await rows.GroupBy(r => r.MatchResultCode)
                .Select(g => new ImportSummaryRow(g.Key, g.Count(), g.Sum(r => r.ReceivedAmount))).ToListAsync(cancellationToken);
            var accepted = await rows.Where(r => r.IsAccepted).GroupBy(_ => 1)
                .Select(g => new { Count = g.Count(), Received = g.Sum(r => r.ReceivedAmount) }).FirstOrDefaultAsync(cancellationToken);

            var filtered = string.IsNullOrEmpty(result) ? rows : rows.Where(r => r.MatchResultCode == result);
            var count = await filtered.CountAsync(cancellationToken);
            page = Math.Max(1, page);
            var list = await filtered.Include(r => r.Receivable).OrderBy(r => r.RowNo)
                .Skip((page - 1) * REVIEW_PAGE_SIZE).Take(REVIEW_PAGE_SIZE).ToListAsync(cancellationToken);

            return new ImportReview
            {
                Batch = batch,
                Summary = ArMatchResultCodes.ALL.Select(code => summary.FirstOrDefault(s => s.ResultCode == code) ?? new(code, 0, 0)).ToList(),
                Rows = list,
                FilteredCount = count,
                Page = page,
                PageSize = REVIEW_PAGE_SIZE,
                AcceptedCount = accepted?.Count ?? 0,
                AcceptedReceived = accepted?.Received ?? 0,
            };
        }

        /// <summary>ติ๊ก/ไม่ติ๊ก รวมแถวตอนยืนยัน (เฉพาะแถวที่มีลูกหนี้ปลายทาง)</summary>
        public async Task<ImportActionResult> SetAcceptedAsync(int rowId, bool accepted, CancellationToken cancellationToken = default)
        {
            var row = await EditableRowAsync(rowId, cancellationToken);
            if (row is null) return new(false, "แก้ไขไม่ได้ — ไฟล์ยืนยันหรือยกเลิกแล้ว");
            if (accepted && !ArMatchResultCodes.ACCEPTABLE.Contains(row.MatchResultCode)) return new(false, "แถวนี้ยังไม่มีลูกหนี้ปลายทาง — ผูกเลขบิลก่อน");
            row.IsAccepted = accepted;
            await _db.SaveChangesAsync(cancellationToken);
            return new(true, accepted ? $"รวมแถว {row.RowNo} แล้ว" : $"ไม่รวมแถว {row.RowNo}");
        }

        /// <summary>ผูกแถวกับลูกหนี้เองด้วยเลขบิล (สิทธิ์เดียวกันเท่านั้น)</summary>
        public async Task<ImportActionResult> ManualMatchAsync(int rowId, string? billNumber, string? userName, CancellationToken cancellationToken = default)
        {
            var row = await EditableRowAsync(rowId, cancellationToken);
            if (row is null) return new(false, "แก้ไขไม่ได้ — ไฟล์ยืนยันหรือยกเลิกแล้ว");
            if (row.MatchResultCode == ArMatchResultCodes.INVALID) return new(false, "แถวนี้ข้อมูลไม่ถูกต้อง — แก้ไฟล์แล้วนำเข้าใหม่");
            var bill = billNumber?.Trim();
            if (string.IsNullOrEmpty(bill)) return new(false, "กรุณาใส่เลขที่บิล");

            var scheme = row.ImportBatch!.SchemeCode;
            var receivable = await _db.ArReceivables.AsNoTracking()
                .Where(r => r.SchemeCode == scheme && r.BillNumber == bill && r.StatusCode != ArStatusCodes.CANCELLED)
                .Select(r => new MatchCandidate(r.Id, r.SchemeCode, r.VisitNumber, r.Hn, r.VisitStartDate, r.DischargeDate, r.BillNumber, r.BilledAmount, r.OutstandingAmount, r.StatusCode))
                .FirstOrDefaultAsync(cancellationToken);
            if (receivable is null) return new(false, $"ไม่พบลูกหนี้เลขบิล {bill} ของสิทธิ์ {scheme}");

            var outcome = RemittanceMatcher.Single(ToRow(row), receivable, RemittanceMatcher.STEP_MANUAL, Settings);
            row.MatchResultCode = outcome.Result;
            row.MatchStep = RemittanceMatcher.STEP_MANUAL;
            row.ReceivableId = receivable.Id;
            row.CandidateIds = null;
            row.Message = Clip($"ผูกเองโดย {userName}" + (outcome.Message is null ? "" : " · " + outcome.Message), 500);
            row.IsAccepted = true;
            await _db.SaveChangesAsync(cancellationToken);
            return new(true, $"ผูกแถว {row.RowNo} กับบิล {bill} แล้ว");
        }

        /// <summary>บันทึกรับชำระจากแถวที่รวม แล้วคำนวณยอด/สถานะลูกหนี้ใหม่</summary>
        public async Task<ImportActionResult> ConfirmAsync(int batchId, string? userName, CancellationToken cancellationToken = default)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
                var batch = await _db.ArImportBatches.Include(b => b.Rows).FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
                if (batch is null) return new ImportActionResult(false, "ไม่พบไฟล์นำเข้า");
                if (batch.StatusCode != ArImportStatusCodes.VALIDATED) return new ImportActionResult(false, "ไฟล์นี้ยืนยันหรือยกเลิกไปแล้ว");

                var rows = batch.Rows.Where(r => r.IsAccepted && ArMatchResultCodes.ACCEPTABLE.Contains(r.MatchResultCode)).ToList();
                if (rows.Count == 0) return new ImportActionResult(false, "ไม่มีแถวที่รวมไว้สำหรับบันทึก");

                var targetIds = rows.SelectMany(r => r.ReceivableId is int id ? [id] : r.Candidates()).Distinct().ToList();
                var receivables = await LoadTrackedAsync(targetIds, cancellationToken);
                var payments = 0;
                foreach (var row in rows)
                {
                    var ids = row.ReceivableId is int single ? [single] : row.Candidates();
                    var targets = ids.Where(receivables.ContainsKey).Select(id => receivables[id]).ToList();
                    if (targets.Count == 0) continue;
                    var amounts = RemittanceMatcher.Allocate(row.ReceivedAmount, targets.Select(t => t.OutstandingAmount).ToList());
                    var denied = RemittanceMatcher.Allocate(row.DeniedAmount, targets.Select(t => t.OutstandingAmount).ToList());
                    for (var i = 0; i < targets.Count; i++)
                    {
                        _db.ArPayments.Add(new ArPayment
                        {
                            ReceivableId = targets[i].Id, ImportBatchId = batch.Id, ImportRowId = row.Id,
                            Amount = amounts[i], DeniedAmount = denied[i], ReasonCode = row.ReasonCode, ReasonText = row.ReasonText,
                            TransferDate = row.TransferDate ?? batch.TransferDate, StatementNo = row.StatementNo ?? batch.StatementNo,
                            SourceCode = ArPaymentSourceCodes.IMPORT,
                        });
                        payments++;
                    }
                }
                await _db.SaveChangesAsync(cancellationToken);

                var now = DateTime.Now; // เวลาไทย
                await RecomputeAsync(receivables.Values, $"บันทึกผลการเรียกเก็บจากไฟล์ #{batch.Id}{Statement(batch.StatementNo)}", now, cancellationToken);
                batch.StatusCode = ArImportStatusCodes.CONFIRMED;
                batch.ConfirmedBy = Clip(userName, 100);
                batch.ConfirmedAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                _db.ChangeTracker.Clear();

                _logger.LogInformation("Remittance import #{Id} confirmed by {User}: {Rows} rows, {Payments} payments", batch.Id, userName, rows.Count, payments);
                return new ImportActionResult(true, $"บันทึกรับชำระ {rows.Count:N0} แถว ({payments:N0} รายการลูกหนี้) แล้ว");
            });
        }

        /// <summary>ยกเลิกไฟล์: รอยืนยัน → ยกเลิกเฉยๆ · ยืนยันแล้ว → ย้อนรับชำระทั้งหมดของไฟล์ (soft delete) และคำนวณลูกหนี้ใหม่</summary>
        public async Task<ImportActionResult> CancelAsync(int batchId, string? userName, CancellationToken cancellationToken = default)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = _db.Database.IsRelational() ? await _db.Database.BeginTransactionAsync(cancellationToken) : null;
                var batch = await _db.ArImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, cancellationToken);
                if (batch is null) return new ImportActionResult(false, "ไม่พบไฟล์นำเข้า");
                if (batch.StatusCode == ArImportStatusCodes.CANCELLED) return new ImportActionResult(false, "ไฟล์นี้ยกเลิกไปแล้ว");

                var now = DateTime.Now;
                var reversed = 0;
                if (batch.StatusCode == ArImportStatusCodes.CONFIRMED)
                {
                    var payments = await _db.ArPayments.Where(p => p.ImportBatchId == batch.Id).ToListAsync(cancellationToken);
                    foreach (var p in payments) p.IsActive = false;
                    reversed = payments.Count;
                    await _db.SaveChangesAsync(cancellationToken);

                    var receivables = await LoadTrackedAsync(payments.Select(p => p.ReceivableId).Distinct().ToList(), cancellationToken);
                    await RecomputeAsync(receivables.Values, $"ยกเลิกการนำเข้าไฟล์ #{batch.Id}{Statement(batch.StatementNo)}", now, cancellationToken);
                }
                batch.StatusCode = ArImportStatusCodes.CANCELLED;
                batch.CancelledBy = Clip(userName, 100);
                batch.CancelledAt = now;
                await _db.SaveChangesAsync(cancellationToken);
                if (transaction is not null) await transaction.CommitAsync(cancellationToken);
                _db.ChangeTracker.Clear();

                _logger.LogInformation("Remittance import #{Id} cancelled by {User}: {Payments} payments reversed", batch.Id, userName, reversed);
                return new ImportActionResult(true, reversed > 0 ? $"ยกเลิกไฟล์และย้อนรับชำระ {reversed:N0} รายการแล้ว" : "ยกเลิกไฟล์แล้ว");
            });
        }

        // ---------------- ภายใน ----------------

        /// <summary>ยอดได้รับ/ปฏิเสธ = ผลรวม ArPayments ที่ active → คงค้าง → สถานะ (ลงประวัติเมื่อสถานะเปลี่ยน)</summary>
        private async Task RecomputeAsync(IEnumerable<ArReceivable> receivables, string reason, DateTime now, CancellationToken cancellationToken)
        {
            var list = receivables.ToList();
            var sums = new Dictionary<int, (decimal Paid, decimal Denied, int Count)>();
            foreach (var chunk in list.Select(r => r.Id).Chunk(KEY_CHUNK))
            {
                var part = await _db.ArPayments.AsNoTracking().Where(p => chunk.Contains(p.ReceivableId))
                    .GroupBy(p => p.ReceivableId)
                    .Select(g => new { Id = g.Key, Paid = g.Sum(p => p.Amount), Denied = g.Sum(p => p.DeniedAmount), Count = g.Count() })
                    .ToListAsync(cancellationToken);
                foreach (var s in part) sums[s.Id] = (s.Paid, s.Denied, s.Count);
            }

            var claimed = await ClaimedReceivableIdsAsync(_db, list.Select(r => r.Id).ToList(), cancellationToken);

            foreach (var r in list)
            {
                var (paid, denied, count) = sums.GetValueOrDefault(r.Id);
                r.PaidAmount = paid;
                r.DeniedAmount = denied;
                r.Recalculate();
                var status = r.DeriveStatus(_options.PaidTolerance, hasResults: count > 0, claimed: claimed.Contains(r.Id));
                r.StatusHistories.Add(new ArStatusHistory { FromStatus = r.StatusCode, ToStatus = status, Reason = Clip($"{reason} · ได้รับรวม {paid:N2} คงค้าง {r.OutstandingAmount:N2}", 500), ChangedAt = now });
                r.StatusCode = status;
            }
        }

        /// <summary>ลูกหนี้ที่อยู่ในชุดเรียกเก็บที่ส่งแล้ว (ใช้คืนสถานะ CLAIMED เมื่อไม่มีผลการเรียกเก็บ)</summary>
        internal static async Task<HashSet<int>> ClaimedReceivableIdsAsync(ApplicationDbContext db, IReadOnlyList<int> ids, CancellationToken cancellationToken)
        {
            var set = new HashSet<int>();
            foreach (var chunk in ids.Chunk(KEY_CHUNK))
            {
                set.UnionWith(await db.ArClaimBatchItems.AsNoTracking()
                    .Where(i => chunk.Contains(i.ReceivableId) && i.ClaimBatch!.StatusCode == ArClaimBatchStatusCodes.SENT && i.ClaimBatch.IsActive)
                    .Select(i => i.ReceivableId).ToListAsync(cancellationToken));
            }
            return set;
        }

        private async Task<Dictionary<int, ArReceivable>> LoadTrackedAsync(IReadOnlyList<int> ids, CancellationToken cancellationToken)
        {
            var result = new Dictionary<int, ArReceivable>();
            foreach (var chunk in ids.Chunk(KEY_CHUNK))
            {
                foreach (var r in await _db.ArReceivables.Where(r => chunk.Contains(r.Id)).ToListAsync(cancellationToken)) result[r.Id] = r;
            }
            return result;
        }

        /// <summary>ลูกหนี้ที่อาจเกี่ยวข้องกับไฟล์: VN/AN ทุกสิทธิ์ (บอกได้ว่าเป็นของสิทธิ์อื่น) + HN ของสิทธิ์นี้</summary>
        private async Task<List<MatchCandidate>> LoadCandidatesAsync(string schemeCode, IReadOnlyList<RemittanceRow> rows, CancellationToken cancellationToken)
        {
            var found = new Dictionary<int, MatchCandidate>();
            var vns = rows.Where(r => r.Error is null && !string.IsNullOrEmpty(r.VisitNumber)).Select(r => r.VisitNumber!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var hns = rows.Where(r => r.Error is null && !string.IsNullOrEmpty(r.Hn)).Select(r => r.Hn!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            foreach (var chunk in vns.Chunk(KEY_CHUNK))
            {
                foreach (var c in await Project(_db.ArReceivables.AsNoTracking().Where(r => r.VisitNumber != null && chunk.Contains(r.VisitNumber))).ToListAsync(cancellationToken))
                    found[c.Id] = c;
            }
            foreach (var chunk in hns.Chunk(KEY_CHUNK))
            {
                foreach (var c in await Project(_db.ArReceivables.AsNoTracking().Where(r => r.Hn != null && chunk.Contains(r.Hn))).ToListAsync(cancellationToken))
                    found[c.Id] = c;
            }
            return [.. found.Values];
        }

        private static IQueryable<MatchCandidate> Project(IQueryable<ArReceivable> q)
            => q.Select(r => new MatchCandidate(r.Id, r.SchemeCode, r.VisitNumber, r.Hn, r.VisitStartDate, r.DischargeDate, r.BillNumber, r.BilledAmount, r.OutstandingAmount, r.StatusCode));

        /// <summary>(ลูกหนี้, statement) ที่บันทึกผลไปแล้ว — กันนำเข้าผลเดิมซ้ำจากคนละไฟล์</summary>
        private async Task<HashSet<(int, string)>> PaidStatementsAsync(IReadOnlyList<int> receivableIds, CancellationToken cancellationToken)
        {
            var set = new HashSet<(int, string)>();
            foreach (var chunk in receivableIds.Chunk(KEY_CHUNK))
            {
                var part = await _db.ArPayments.AsNoTracking().Where(p => chunk.Contains(p.ReceivableId) && p.StatementNo != null)
                    .Select(p => new { p.ReceivableId, p.StatementNo }).ToListAsync(cancellationToken);
                foreach (var p in part) set.Add((p.ReceivableId, p.StatementNo!));
            }
            return set;
        }

        private async Task<ArImportRow?> EditableRowAsync(int rowId, CancellationToken cancellationToken)
        {
            var row = await _db.ArImportRows.Include(r => r.ImportBatch).FirstOrDefaultAsync(r => r.Id == rowId, cancellationToken);
            return row?.ImportBatch?.StatusCode == ArImportStatusCodes.VALIDATED ? row : null;
        }

        private static IReadOnlyList<int> Targets(MatchOutcome o) => o.ReceivableId is int id ? [id] : o.CandidateIds;

        private static ArImportRow ToEntity(RemittanceRow r, MatchOutcome o) => new()
        {
            RowNo = r.RowNo, VisitNumber = r.VisitNumber, Hn = r.Hn, VisitDate = r.VisitDate, DischargeDate = r.DischargeDate, BillNumber = r.BillNumber,
            ClaimedAmount = r.ClaimedAmount, ReceivedAmount = r.ReceivedAmount, DeniedAmount = r.DeniedAmount, ReasonCode = r.ReasonCode, ReasonText = r.ReasonText,
            StatementNo = r.StatementNo, TransferDate = r.TransferDate, Note = r.Note,
            MatchResultCode = o.Result, MatchStep = o.Step, ReceivableId = o.ReceivableId,
            CandidateIds = o.CandidateIds.Count == 0 ? null : Clip(string.Join(',', o.CandidateIds), 400),
            Message = Clip(o.Message, 500),
            IsAccepted = ArMatchResultCodes.AUTO_ACCEPT.Contains(o.Result),
        };

        private static RemittanceRow ToRow(ArImportRow r) => new(r.RowNo, r.VisitNumber, r.Hn, r.VisitDate, r.DischargeDate, r.BillNumber, r.ClaimedAmount,
            r.ReceivedAmount, r.DeniedAmount, r.ReasonCode, r.ReasonText, r.StatementNo, r.TransferDate, r.Note);

        private static void SetResult(ArImportRow row, string result, string message)
        {
            row.MatchResultCode = result;
            row.Message = Clip(message, 500);
            row.IsAccepted = false;
        }

        private static string Statement(string? statementNo) => string.IsNullOrEmpty(statementNo) ? "" : $" (statement {statementNo})";

        private static string? Clip(string? value, int max) => value is null ? null : value.Length <= max ? value : value[..max];
    }
}
