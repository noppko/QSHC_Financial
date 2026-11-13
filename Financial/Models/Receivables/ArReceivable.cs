using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financial.Models.Receivables
{
    /// <summary>
    /// ลูกหนี้สิทธิ 1 รายการ = 1 บิลเครดิตใน HIS (PatientBill.IsPaymentComplete = 'N') ของ 1 ใน 4 สิทธิ์
    /// ดึงจาก HIS (อ่านอย่างเดียว) มาเก็บที่ QSHC_Financial — ยอดรับชำระมาจากไฟล์ของหน่วยงาน (ระยะ 2)
    /// </summary>
    public class ArReceivable : BaseEntity
    {
        /// <summary>PatientBill.UID ใน HIS — คีย์ upsert</summary>
        public long HisBillUid { get; set; }

        [MaxLength(30)]
        public string BillNumber { get; set; } = string.Empty;

        /// <summary>PatientVisit.UID — ใช้ภายในเท่านั้น ห้ามส่งไปหน้าเว็บ</summary>
        public long HisVisitUid { get; set; }

        /// <summary>VN/AN (PatientVisitID.Identifier สูงสุด 10)</summary>
        [MaxLength(20)]
        public string? VisitNumber { get; set; }

        /// <summary>HN (Patient.PASID 4–9 ตัว) — PHI แสดงเฉพาะ Admin พร้อม audit</summary>
        [MaxLength(20)]
        public string? Hn { get; set; }

        /// <summary>PATIENT_TYPE: O / I (ตามประเภท visit VN/AN)</summary>
        [MaxLength(1)]
        public string PatientTypeCode { get; set; } = PatientTypeCodes.OPD;

        [Column(TypeName = "datetime2(0)")]
        public DateTime? VisitStartDate { get; set; }

        /// <summary>วันจำหน่าย (PatientVisit.EndDttm) — ใช้กับ IPD</summary>
        [Column(TypeName = "datetime2(0)")]
        public DateTime? DischargeDate { get; set; }

        /// <summary>วันออกบิล (PatientBill.BillGeneratedDttm) — ใช้นับอายุหนี้</summary>
        [Column(TypeName = "datetime2(0)")]
        public DateTime BillDate { get; set; }

        /// <summary>PAYOR_SCHEME: NHSO / CSMBS / LGO / SSO</summary>
        [MaxLength(10)]
        public string SchemeCode { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? PayorCode { get; set; }

        [MaxLength(200)]
        public string? PayorName { get; set; }

        /// <summary>PYRACAT ใน HIS</summary>
        [MaxLength(10)]
        public string? PayorCategoryCode { get; set; }

        [MaxLength(400)]
        public string? AgreementName { get; set; }

        /// <summary>นอกเขต (ตามชื่อเงื่อนไข) — แสดงเฉพาะ สปสช.</summary>
        public bool IsOutOfZone { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BilledAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DeniedAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal WriteOffAmount { get; set; }

        /// <summary>= BilledAmount − PaidAmount − DeniedAmount − WriteOffAmount (คำนวณใหม่ทุกครั้งด้วย <see cref="Recalculate"/>)</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingAmount { get; set; }

        /// <summary>AR_STATUS</summary>
        [MaxLength(20)]
        public string StatusCode { get; set; } = ArStatusCodes.OPEN;

        [Column(TypeName = "datetime2(0)")]
        public DateTime LastSyncedAt { get; set; }

        public List<ArStatusHistory> StatusHistories { get; set; } = [];

        public void Recalculate() => OutstandingAmount = BilledAmount - PaidAmount - DeniedAmount - WriteOffAmount;
    }

    /// <summary>ประวัติการเปลี่ยนสถานะ/ยอดของลูกหนี้ (ตรวจย้อนได้ว่าใคร ทำอะไร เมื่อไร)</summary>
    public class ArStatusHistory : BaseEntity
    {
        public int ReceivableId { get; set; }

        public ArReceivable? Receivable { get; set; }

        [MaxLength(20)]
        public string? FromStatus { get; set; }

        [MaxLength(20)]
        public string ToStatus { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Reason { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime ChangedAt { get; set; } = DateTime.Now; // เวลาไทย
    }
}
