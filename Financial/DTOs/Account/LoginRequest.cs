using System.ComponentModel.DataAnnotations;

namespace Financial.DTOs.Account
{
    /// <summary>
    /// DTO สำหรับรับข้อมูลการ Login จากฟอร์ม
    /// </summary>
    public class LoginRequest
    {
        [Required(ErrorMessage = "กรุณากรอกชื่อผู้ใช้")]
        [Display(Name = "ชื่อผู้ใช้")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "กรุณากรอกรหัสผ่าน")]
        [DataType(DataType.Password)]
        [Display(Name = "รหัสผ่าน")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "จดจำฉันไว้")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
