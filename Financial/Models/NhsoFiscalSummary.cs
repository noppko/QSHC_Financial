using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Financial.Models
{
    /// <summary>
    /// ผลรวมรายงานสิทธิ สปสช. ตามปีงบประมาณ (ตาราง NhsoFiscalSummaries ใน QSHC_Financial)
    /// ประมวลผลจาก HIS HealthObject แล้วเก็บแยกไว้ที่นี่ — ไม่มีข้อมูลผู้ป่วยรายบุคคล
    /// 1 แถว = ปีงบ × ประเภทผู้ป่วย × กลุ่มผู้จ่าย × เงื่อนไขการเรียกเก็บ
    /// </summary>
    public class NhsoFiscalSummary : BaseEntity
    {
        /// <summary>ปีงบประมาณ พ.ศ. (1 ต.ค. – 30 ก.ย.)</summary>
        public int FiscalYear { get; set; }

        /// <summary>ประเภทบิลใน HIS — รหัส ReferenceValues category PATIENT_TYPE: O = OPD, I = IPD</summary>
        [Required]
        [MaxLength(10)]
        public string PatientTypeCode { get; set; } = string.Empty;

        /// <summary>
        /// ประเภทผู้ป่วยของสิทธิ์จาก HIS (PayorAgreement.ENTYPUID → ReferenceValue โดเมน ENTYP): OUPAT / INPAT / AEPAT / null = สิทธิ์ไม่ได้ระบุ
        /// เก็บรหัสดิบ — จับคู่เป็น O/I ตอนแสดงรายงานจาก config Reports:Nhso:AgreementEncounterTypes
        /// </summary>
        [MaxLength(20)]
        public string? AgreementEncounterCode { get; set; }

        /// <summary>กลุ่มผู้จ่ายใน HIS (PayorDetail.PYRACATUID): A / E / M</summary>
        [Required]
        [MaxLength(20)]
        public string PayorCategoryCode { get; set; } = string.Empty;

        /// <summary>เงื่อนไขการเรียกเก็บ (PayorAgreement.Name ใน HIS)</summary>
        [Required]
        [MaxLength(400)]
        public string AgreementName { get; set; } = string.Empty;

        public int Visits { get; set; }

        public int Bills { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetAmount { get; set; }

        /// <summary>เวลาที่ประมวลผลจาก HIS (เวลาไทย)</summary>
        public DateTime RefreshedAt { get; set; }
    }
}
