using System.ComponentModel.DataAnnotations;

namespace Financial.Models
{
    /// <summary>
    /// ตารางเก็บค่าอ้างอิงทั้งหมดของระบบ แทนการใช้ Enum
    /// เช่น TITLE (นาย, นาง, นางสาว), GENDER (ชาย, หญิง), BLOOD_TYPE, etc.
    /// </summary>
    public class ReferenceValue : BaseEntity
    {
        /// <summary>
        /// หมวดหมู่ของค่าอ้างอิง เช่น GENDER, BLOOD_TYPE, TITLE
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// รหัสของค่าอ้างอิง เช่น MALE, FEMALE สำหรับ Category = GENDER
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// ชื่อภาษาไทย
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string NameTH { get; set; } = string.Empty;

        /// <summary>
        /// ชื่อภาษาอังกฤษ (optional)
        /// </summary>
        [MaxLength(200)]
        public string? NameEN { get; set; }

        /// <summary>
        /// รายละเอียดเพิ่มเติม
        /// </summary>
        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>
        /// ลำดับการแสดงผล (เรียงจากน้อยไปมาก)
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// เป็นค่าระดับระบบหรือไม่ (true = ห้ามลบ/แก้ไขผ่านหน้าจอ)
        /// </summary>
        public bool IsSystem { get; set; }

        /// <summary>
        /// หมวดหมู่ Parent สำหรับโครงสร้างแบบ hierarchical
        /// </summary>
        [MaxLength(100)]
        public string? ParentCategory { get; set; }

        /// <summary>
        /// FK อ้างอิงตัวเอง สำหรับ Parent-Children relationship
        /// </summary>
        public int? ParentId { get; set; }

        /// <summary>
        /// ข้อมูลเสริมรูปแบบ JSON
        /// </summary>
        [MaxLength(2000)]
        public string? Metadata { get; set; }

        // Navigation property สำหรับ self-referencing
        public virtual ReferenceValue? Parent { get; set; }
        public virtual ICollection<ReferenceValue>? Children { get; set; }
    }
}
