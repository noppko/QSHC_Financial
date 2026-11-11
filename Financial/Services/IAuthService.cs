using Financial.DTOs.Account;

namespace Financial.Services
{
    /// <summary>
    /// Interface สำหรับ Authentication Service
    /// </summary>
    public interface IAuthService
    {
        /// <summary>
        /// ทำการ Login ผ่าน External Auth API
        /// </summary>
        /// <param name="username">ชื่อผู้ใช้</param>
        /// <param name="password">รหัสผ่าน</param>
        /// <param name="cancellationToken">ยกเลิกเมื่อผู้ใช้ปิดหน้า/ยกเลิก request</param>
        /// <returns>ผลตอบกลับจาก API, null ถ้าเชื่อมต่อ API ไม่ได้</returns>
        Task<AuthResponse?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken = default);
    }
}
