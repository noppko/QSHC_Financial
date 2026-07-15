using System.ComponentModel.DataAnnotations;

namespace Financial.Models
{
    /// <summary>
    /// Entity พื้นฐานที่ทุก Entity ต้อง inherit เพื่อมี audit fields และ soft delete
    /// </summary>
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [Required]
        [MaxLength(100)]
        public string CreatedBy { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? UpdatedBy { get; set; }

        /// <summary>
        /// ใช้สำหรับ Soft Delete - true = ใช้งานอยู่, false = ถูกลบ
        /// </summary>
        public bool IsActive { get; set; } = true;
    }
}
