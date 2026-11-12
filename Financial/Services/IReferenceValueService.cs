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
        /// ชื่อภาษาไทยของรหัส (ใช้ cache ของ category) — อ่านฐานไม่ได้หรือไม่พบรหัส คืนค่า fallback
        /// </summary>
        Task<string> GetNameAsync(string category, string code, string fallback);

        /// <summary>
        /// เพิ่มหรืออัปเดต Reference Value
        /// </summary>
        Task<bool> SaveAsync(ReferenceValue value);

        /// <summary>
        /// เพิ่มหรืออัปเดต พร้อมเหตุผลเมื่อไม่สำเร็จ (ค่าระบบ, รหัสซ้ำ, รูปแบบไม่ถูกต้อง)
        /// แก้ไข = โหลดแถวเดิมแล้วคัดลอกเฉพาะช่องที่แก้ได้ (ไม่ทับ CreatedAt/CreatedBy)
        /// </summary>
        Task<ReferenceSaveResult> TrySaveAsync(ReferenceValue value);

        /// <summary>หมวดทั้งหมดพร้อมจำนวนรายการที่ใช้งานอยู่ (ไม่ใช้ cache — สำหรับหน้าจัดการ)</summary>
        Task<List<ReferenceCategorySummary>> GetCategoriesAsync();

        /// <summary>อ่าน 1 รายการ (เฉพาะที่ใช้งานอยู่)</summary>
        Task<ReferenceValue?> GetByIdAsync(int id);

        /// <summary>
        /// จัดลำดับใหม่ตามลำดับ id ที่ส่งมา (DisplayOrder = 10, 20, 30, ...) — id ต้องเป็นของหมวดนี้ครบทุกตัว
        /// </summary>
        Task<bool> ReorderAsync(string category, IReadOnlyList<int> orderedIds);

        /// <summary>
        /// ลบ Reference Value (Soft Delete)
        /// </summary>
        Task<bool> DeleteAsync(int id);

        /// <summary>
        /// Invalidate cache สำหรับ Category ที่ระบุ
        /// </summary>
        void InvalidateCache(string category);
    }

    /// <summary>ผลการบันทึก Reference Value</summary>
    public sealed record ReferenceSaveResult(bool Ok, string? Error = null)
    {
        public static readonly ReferenceSaveResult SUCCESS = new(true);
        public static ReferenceSaveResult Fail(string error) => new(false, error);
    }

    /// <summary>สรุปหมวดของ Reference Value</summary>
    public sealed record ReferenceCategorySummary(string Category, int Count, int SystemCount);
}
