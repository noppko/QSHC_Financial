using Financial.Models;

namespace Financial.Services
{
    /// <summary>
    /// Interface สำหรับจัดการ Reference Values
    /// </summary>
    public interface IReferenceValueService
    {
        /// <summary>
        /// ดึงค่าทั้งหมดตาม Category
        /// </summary>
        Task<List<ReferenceValue>> GetByCategoryAsync(string category);

        /// <summary>
        /// ดึงค่าตาม Category และ Code
        /// </summary>
        Task<ReferenceValue?> GetByCodeAsync(string category, string code);

        /// <summary>
        /// เพิ่มหรืออัปเดต Reference Value
        /// </summary>
        Task<bool> SaveAsync(ReferenceValue value);

        /// <summary>
        /// ลบ Reference Value (Soft Delete)
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Invalidate cache สำหรับ Category ที่ระบุ
        /// </summary>
        void InvalidateCache(string category);
    }
}
