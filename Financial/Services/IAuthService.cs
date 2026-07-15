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
        /// <returns>ข้อมูลผู้ใช้ถ้า login สำเร็จ, null ถ้าล้มเหลว</returns>
        Task<AuthResponse?> AuthenticateAsync(string username, string password);
    }
}
