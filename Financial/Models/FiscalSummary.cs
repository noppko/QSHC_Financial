using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financial.Models
{
    /// <summary>
    /// ผลรวมตามเงื่อนไขการเรียกเก็บ ของรายงานสิทธิ์ตามปีงบประมาณ (ตาราง FiscalPayorSummaries ใน QSHC_Financial)
    /// ประมวลผลจาก HIS HealthObject แล้วเก็บไว้ที่นี่ — ไม่มีข้อมูลผู้ป่วยรายบุคคล
    /// 1 แถว = ปีงบ × สิทธิ์ × ประเภทผู้ป่วย × กลุ่มผู้จ่าย × เงื่อนไขการเรียกเก็บ × ประเภท visit
    /// ใช้แสดงตาราง "เงื่อนไขการเรียกเก็บที่พบ" เพื่อตรวจการจัดในเขต/นอกเขต
    /// </summary>
    public class FiscalPayorSummary : BaseEntity
    {
        /// <summary>ปีงบประมาณ พ.ศ. (1 ต.ค. – 30 ก.ย.)</summary>
        public int FiscalYear { get; set; }

        /// <summary>สิทธิ์ — รหัส ReferenceValues category PAYOR_SCHEME (NHSO / CSMBS / SSO / LGO)</summary>
        [Required]
        [MaxLength(20)]
        public string SchemeCode { get; set; } = string.Empty;

        /// <summary>ประเภทผู้ป่วยจาก visit (VN = O, AN = I) — รหัส ReferenceValues category PATIENT_TYPE</summary>
        [Required]
        [MaxLength(10)]
        public string PatientTypeCode { get; set; } = string.Empty;

        /// <summary>ประเภท visit ใน HIS (PatientVisit.ENTYPUID → ReferenceValue ENTYP): OUPAT / INPAT / AEPAT / null</summary>
        [MaxLength(20)]
        public string? VisitEncounterCode { get; set; }

        /// <summary>true = ประเภทบิลใน HIS ไม่ตรงกับประเภท visit (เช่น บิล OPD ของ admission)</summary>
        public bool MovedFromBillSide { get; set; }

        /// <summary>กลุ่มผู้จ่ายใน HIS (PayorDetail.PYRACATUID) เช่น A, B, C</summary>
        [Required]
        [MaxLength(20)]
        public string PayorCategoryCode { get; set; } = string.Empty;

        /// <summary>เงื่อนไขการเรียกเก็บ (PayorAgreement.Name ใน HIS)</summary>
        [Required]
        [MaxLength(400)]
        public string AgreementName { get; set; } = string.Empty;

        /// <summary>จำนวนผู้ป่วย (คน ไม่ซ้ำ) ภายในเงื่อนไขนี้</summary>
        public int Patients { get; set; }

        public int Visits { get; set; }

        public int Bills { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        /// <summary>เวลาที่ประมวลผลจาก HIS (เวลาไทย)</summary>
        public DateTime RefreshedAt { get; set; }
    }

    /// <summary>
    /// ยอดรวมของรายงานสิทธิ์ตามปีงบประมาณ (ตาราง FiscalSchemeTotals ใน QSHC_Financial)
    /// 1 แถว = ปีงบ × สิทธิ์ × ประเภทผู้ป่วย × เขต (IN / OUT / ALL)
    /// จำนวนผู้ป่วยนับคนไม่ซ้ำ จึงคำนวณตอนประมวลผล (รวมจากรายเงื่อนไขไม่ได้ — คนเดียวมีได้หลายเงื่อนไข)
    /// </summary>
    public class FiscalSchemeTotal : BaseEntity
    {
        public int FiscalYear { get; set; }

        /// <summary>รหัส ReferenceValues category PAYOR_SCHEME</summary>
        [Required]
        [MaxLength(20)]
        public string SchemeCode { get; set; } = string.Empty;

        /// <summary>รหัส ReferenceValues category PATIENT_TYPE (O / I)</summary>
        [Required]
        [MaxLength(10)]
        public string PatientTypeCode { get; set; } = string.Empty;

        /// <summary>รหัส ReferenceValues category FISCAL_ZONE (IN / OUT / ALL)</summary>
        [Required]
        [MaxLength(10)]
        public string ZoneCode { get; set; } = string.Empty;

        /// <summary>จำนวนผู้ป่วย (คน ไม่ซ้ำ)</summary>
        public int Patients { get; set; }

        public int Visits { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        public DateTime RefreshedAt { get; set; }
    }
}
