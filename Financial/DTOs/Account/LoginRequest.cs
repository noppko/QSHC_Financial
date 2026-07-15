using System.ComponentModel.DataAnnotations;

namespace Financial.DTOs.Account
{
    public class LoginRequest
    {
        [Required]
        public string Username { get; set; }

        [Required(ErrorMessage = "กรุณาใส่รหัสผ่าน")]
        [DataType(DataType.Password)]
        [Display(Name = "รหัสผ่าน")]
        public string Password { get; set; }

        [Display(Name = "จดจำการเข้าสู่ระบบ")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }
}
