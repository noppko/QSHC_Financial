using System.ComponentModel.DataAnnotations;

namespace Financial.Models
{
    /// <summary>
    /// การตั้งค่าที่ผู้ดูแลระบบแก้ได้จากหน้าเว็บ (ตาราง AppSettings ใน QSHC_Financial) — เก็บแบบ Key/Value
    /// </summary>
    public class AppSetting : BaseEntity
    {
        /// <summary>ชื่อการตั้งค่า เช่น MENU_HIDDEN_GROUPS (unique)</summary>
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        /// <summary>ค่า (ข้อความหรือ JSON)</summary>
        [MaxLength(4000)]
        public string? Value { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }
    }

    /// <summary>
    /// Key ของตาราง AppSettings
    /// </summary>
    public static class AppSettingKeys
    {
        /// <summary>JSON array ของ key กลุ่มเมนูที่ซ่อน เช่น ["interface"]</summary>
        public const string MENU_HIDDEN_GROUPS = "MENU_HIDDEN_GROUPS";
    }
}
