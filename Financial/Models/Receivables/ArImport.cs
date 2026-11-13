using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financial.Models.Receivables
{
    /// <summary>
    /// ไฟล์ผลการเรียกเก็บ 1 ไฟล์ที่นำเข้า (1 ไฟล์ = 1 สิทธิ์) — ไม่เก็บไฟล์ต้นฉบับ (มี PHI) เก็บเฉพาะแถวที่อ่านได้
    /// สถานะ AR_IMPORT_STATUS: VALIDATED (จับคู่แล้ว รอยืนยัน) → CONFIRMED (บันทึกรับชำระแล้ว) / CANCELLED
    /// </summary>
    public class ArImportBatch : BaseEntity
    {
        [MaxLength(10)]
        public string SchemeCode { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? StatementNo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? TransferDate { get; set; }

        /// <summary>ชุดเรียกเก็บที่ไฟล์นี้ตอบกลับ (อ่านจาก template ที่ส่งออกจากชุด) — ใช้ติดตามเท่านั้น</summary>
        public int? ClaimBatchId { get; set; }

        [MaxLength(260)]
        public string FileName { get; set; } = string.Empty;

        /// <summary>SHA-256 ของไฟล์ (hex) — กันนำเข้าไฟล์เดิมซ้ำ</summary>
        [MaxLength(64)]
        public string FileSha256 { get; set; } = string.Empty;

        public int RowCount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalReceived { get; set; }

        [MaxLength(20)]
        public string StatusCode { get; set; } = ArImportStatusCodes.VALIDATED;

        [MaxLength(100)]
        public string? ConfirmedBy { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime? ConfirmedAt { get; set; }

        [MaxLength(100)]
        public string? CancelledBy { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime? CancelledAt { get; set; }

        public List<ArImportRow> Rows { get; set; } = [];
    }

    /// <summary>1 แถวในไฟล์ผลการเรียกเก็บ พร้อมผลการจับคู่</summary>
    public class ArImportRow : BaseEntity
    {
        public int ImportBatchId { get; set; }

        public ArImportBatch? ImportBatch { get; set; }

        /// <summary>เลขแถวใน Excel (แถว 2 = ข้อมูลแถวแรก)</summary>
        public int RowNo { get; set; }

        [MaxLength(20)]
        public string? VisitNumber { get; set; }

        /// <summary>HN — PHI แสดงเฉพาะ Admin</summary>
        [MaxLength(20)]
        public string? Hn { get; set; }

        [Column(TypeName = "date")]
        public DateTime? VisitDate { get; set; }

        [Column(TypeName = "date")]
        public DateTime? DischargeDate { get; set; }

        [MaxLength(30)]
        public string? BillNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal? ClaimedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ReceivedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeniedAmount { get; set; }

        [MaxLength(50)]
        public string? ReasonCode { get; set; }

        [MaxLength(500)]
        public string? ReasonText { get; set; }

        [MaxLength(100)]
        public string? StatementNo { get; set; }

        [Column(TypeName = "date")]
        public DateTime? TransferDate { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        /// <summary>AR_MATCH_RESULT</summary>
        [MaxLength(20)]
        public string MatchResultCode { get; set; } = ArMatchResultCodes.NOT_FOUND;

        /// <summary>1 = VN/AN, 2 = HN + วันที่, 3 = HN + วันที่ใกล้ + ยอดใกล้, MANUAL = ผูกเอง</summary>
        [MaxLength(10)]
        public string? MatchStep { get; set; }

        /// <summary>ลูกหนี้ที่จับคู่ได้ (1 รายการ)</summary>
        public int? ReceivableId { get; set; }

        public ArReceivable? Receivable { get; set; }

        /// <summary>กรณี MULTIPLE: Id ลูกหนี้ที่จะกระจายยอด (คั่นด้วย ,)</summary>
        [MaxLength(400)]
        public string? CandidateIds { get; set; }

        [MaxLength(500)]
        public string? Message { get; set; }

        /// <summary>รวมแถวนี้ตอนยืนยัน — MATCHED/AMOUNT_DIFF/DENIED รวมอัตโนมัติ, SUGGESTED/MULTIPLE ต้องให้คนติ๊กยอมรับ</summary>
        public bool IsAccepted { get; set; }

        public IReadOnlyList<int> Candidates()
            => string.IsNullOrWhiteSpace(CandidateIds) ? [] : CandidateIds.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToList();
    }

    /// <summary>การรับชำระ/ผลการเรียกเก็บที่บันทึกให้ลูกหนี้ 1 รายการ (ยกเลิก = IsActive false)</summary>
    public class ArPayment : BaseEntity
    {
        public int ReceivableId { get; set; }

        public ArReceivable? Receivable { get; set; }

        public int? ImportBatchId { get; set; }

        public int? ImportRowId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeniedAmount { get; set; }

        [MaxLength(50)]
        public string? ReasonCode { get; set; }

        [MaxLength(500)]
        public string? ReasonText { get; set; }

        [Column(TypeName = "date")]
        public DateTime? TransferDate { get; set; }

        [MaxLength(100)]
        public string? StatementNo { get; set; }

        /// <summary>AR_PAYMENT_SOURCE</summary>
        [MaxLength(20)]
        public string SourceCode { get; set; } = ArPaymentSourceCodes.IMPORT;
    }
}
