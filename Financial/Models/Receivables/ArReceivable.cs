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

        /// <summary>
        /// = BilledAmount − PaidAmount − WriteOffAmount (คำนวณใหม่ทุกครั้งด้วย <see cref="Recalculate"/>)
        /// ยอดปฏิเสธ (DeniedAmount) ไม่หักออก — ยังเป็นหนี้ที่ต้องติดตาม/อุทธรณ์ จนกว่าจะอนุมัติตัดหนี้
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal OutstandingAmount { get; set; }

        /// <summary>AR_STATUS</summary>
        [MaxLength(20)]
        public string StatusCode { get; set; } = ArStatusCodes.OPEN;

        [Column(TypeName = "datetime2(0)")]
        public DateTime LastSyncedAt { get; set; }

        /// <summary>
        /// ต้องให้คนตรวจ: บิลใน HIS เปลี่ยน/หายไป หลังส่งเรียกเก็บหรือมีผลการเรียกเก็บแล้ว (เช่น เภสัชคีย์ยาเพิ่มหลังจำหน่าย แล้วแก้บิล)
        /// ระบบไม่เปลี่ยนสถานะเอง — ล้างธงเมื่อเจ้าหน้าที่กด "ตรวจแล้ว" หรือบิลกลับมาตรงตามเดิม
        /// </summary>
        [MaxLength(300)]
        public string? ReviewReason { get; set; }

        [Column(TypeName = "datetime2(0)")]
        public DateTime? ReviewFlaggedAt { get; set; }

        /// <summary>ส่งเรียกเก็บแล้วหรือมีผลการเรียกเก็บแล้ว — ยอด/บิลเปลี่ยนต้องให้คนตรวจ</summary>
        public bool IsBeyondOpen => StatusCode != ArStatusCodes.OPEN || PaidAmount > 0 || DeniedAmount > 0;

        public List<ArStatusHistory> StatusHistories { get; set; } = [];

        public void Recalculate() => OutstandingAmount = BilledAmount - PaidAmount - WriteOffAmount;

        /// <summary>
        /// สถานะตามยอด (ยกเว้น CANCELLED / WRITE_OFF ที่คงไว้): ไม่มีผลการเรียกเก็บ = OPEN ·
        /// คงค้าง ≤ เกณฑ์ปัดเศษ = PAID · ได้บางส่วน = PARTIAL · ไม่ได้เลยแต่มีผลปฏิเสธ = DENIED
        /// </summary>
        /// <param name="claimed">อยู่ในชุดเรียกเก็บที่ส่งแล้ว — ไม่มีผลการเรียกเก็บ (เช่น ยกเลิกไฟล์นำเข้า) กลับเป็น CLAIMED ไม่ใช่ OPEN</param>
        public string DeriveStatus(decimal tolerance, bool hasResults, bool claimed = false)
        {
            if (StatusCode is ArStatusCodes.CANCELLED or ArStatusCodes.WRITE_OFF) return StatusCode;
            if (!hasResults) return StatusCode == ArStatusCodes.APPEAL ? StatusCode : claimed || StatusCode == ArStatusCodes.CLAIMED ? ArStatusCodes.CLAIMED : ArStatusCodes.OPEN;
            if (OutstandingAmount <= tolerance) return ArStatusCodes.PAID;
            return PaidAmount > 0 ? ArStatusCodes.PARTIAL : ArStatusCodes.DENIED;
        }
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
