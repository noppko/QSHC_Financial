using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financial.Models.Receivables
{
    /// <summary>
    /// ชุดเรียกเก็บ: กลุ่มลูกหนี้ของ 1 สิทธิ์ที่ส่งเรียกเก็บพร้อมกัน
    /// สถานะ AR_CLAIM_BATCH_STATUS: DRAFT (จัดชุด) → SENT (ส่งแล้ว ลูกหนี้เป็น CLAIMED) / CANCELLED
    /// </summary>
    public class ArClaimBatch : BaseEntity
    {
        /// <summary>เลขชุด เช่น NHSO-I-2569-11-0001</summary>
        [MaxLength(40)]
        public string BatchNo { get; set; } = string.Empty;

        [MaxLength(10)]
        public string SchemeCode { get; set; } = string.Empty;

        /// <summary>O / I / null = ทั้งสองแบบ</summary>
        [MaxLength(1)]
        public string? PatientTypeCode { get; set; }

        /// <summary>กรองผู้จ่าย (คำในชื่อ) ที่ใช้ตอนจัดชุด</summary>
        [MaxLength(100)]
        public string? PayorFilter { get; set; }

        [Column(TypeName = "date")]
        public DateTime? PeriodFrom { get; set; }

        [Column(TypeName = "date")]
        public DateTime? PeriodTo { get; set; }

        [MaxLength(20)]
        public string StatusCode { get; set; } = ArClaimBatchStatusCodes.DRAFT;

        [Column(TypeName = "date")]
        public DateTime? SentDate { get; set; }

        /// <summary>เลขหนังสือนำส่ง / เลขอ้างอิงของหน่วยงาน</summary>
        [MaxLength(100)]
        public string? ReferenceDocNo { get; set; }

        [MaxLength(100)]
        public string? SentBy { get; set; }

        public int ItemCount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        public List<ArClaimBatchItem> Items { get; set; } = [];
    }

    /// <summary>ลูกหนี้ 1 รายการในชุดเรียกเก็บ (ยอดที่เรียกเก็บ ณ วันจัดชุด)</summary>
    public class ArClaimBatchItem : BaseEntity
    {
        public int ClaimBatchId { get; set; }

        public ArClaimBatch? ClaimBatch { get; set; }

        public int ReceivableId { get; set; }

        public ArReceivable? Receivable { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ClaimedAmount { get; set; }
    }
}
