using Financial.Data;
using Financial.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace Financial.Services
{
    /// <summary>
    /// Service สำหรับจัดการ Reference Values พร้อม caching
    /// </summary>
    public partial class ReferenceValueService : IReferenceValueService
    {
        /// <summary>ลำดับห่างกันทีละ 10 เพื่อแทรกรายการใหม่ระหว่างกลางได้</summary>
        public const int DISPLAY_ORDER_STEP = 10;

        private readonly ApplicationDbContext _context;
        private readonly IMemoryCache _cache;
        private readonly ILogger<ReferenceValueService> _logger;
        private const int CACHE_EXPIRATION_MINUTES = 60;

        public ReferenceValueService(
            ApplicationDbContext context,
            IMemoryCache cache,
            ILogger<ReferenceValueService> logger)
        {
            _context = context;
            _cache = cache;
            _logger = logger;
        }

        /// <summary>
        /// ดึงค่าทั้งหมดตาม Category พร้อม caching
        /// </summary>
        public async Task<List<ReferenceValue>> GetByCategoryAsync(string category)
        {
            var cacheKey = $"RefValue_{category}";

            if (_cache.TryGetValue(cacheKey, out List<ReferenceValue>? cachedValues) && cachedValues != null)
            {
                return cachedValues;
            }

            var values = await _context.ReferenceValues
                .AsNoTracking()
                .Where(rv => rv.Category == category && rv.IsActive)
                .OrderBy(rv => rv.DisplayOrder)
                .ThenBy(rv => rv.NameTH)
                .ToListAsync();

            _cache.Set(cacheKey, values, TimeSpan.FromMinutes(CACHE_EXPIRATION_MINUTES));

            return values;
        }

        /// <summary>
        /// ดึงค่าตาม Category และ Code
        /// </summary>
        public async Task<ReferenceValue?> GetByCodeAsync(string category, string code)
        {
            return await _context.ReferenceValues
                .AsNoTracking()
                .FirstOrDefaultAsync(rv => rv.Category == category && rv.Code == code && rv.IsActive);
        }

        /// <summary>
        /// ชื่อภาษาไทยของรหัส (ใช้ cache ของ category) — อ่านฐานไม่ได้หรือไม่พบรหัส คืนค่า fallback
        /// </summary>
        public async Task<string> GetNameAsync(string category, string code, string fallback)
        {
            try
            {
                var values = await GetByCategoryAsync(category);
                return values.FirstOrDefault(v => string.Equals(v.Code, code, StringComparison.OrdinalIgnoreCase))?.NameTH ?? fallback;
            }
            catch (Exception ex) when (ex is Microsoft.Data.SqlClient.SqlException or InvalidOperationException)
            {
                // ฐานใช้งานไม่ได้: จำผลว่างไว้ 1 นาที เพื่อไม่ให้ทุกหน้าต้องรอเชื่อมต่อฐานซ้ำ
                _logger.LogWarning(ex, "Cannot read ReferenceValues {Category} — using fallback name", category);
                _cache.Set($"RefValue_{category}", new List<ReferenceValue>(), TimeSpan.FromMinutes(1));
                return fallback;
            }
        }

        /// <summary>
        /// เพิ่มหรืออัปเดต Reference Value
        /// </summary>
        public async Task<bool> SaveAsync(ReferenceValue value) => (await TrySaveAsync(value)).Ok;

        /// <summary>
        /// เพิ่มหรืออัปเดต พร้อมเหตุผลเมื่อไม่สำเร็จ
        /// </summary>
        public async Task<ReferenceSaveResult> TrySaveAsync(ReferenceValue value)
        {
            value.Category = (value.Category ?? "").Trim().ToUpperInvariant();
            value.Code = (value.Code ?? "").Trim();
            value.NameTH = (value.NameTH ?? "").Trim();
            value.NameEN = string.IsNullOrWhiteSpace(value.NameEN) ? null : value.NameEN.Trim();
            value.Description = string.IsNullOrWhiteSpace(value.Description) ? null : value.Description.Trim();

            if (!CategoryPattern().IsMatch(value.Category))
                return ReferenceSaveResult.Fail("หมวดต้องเป็นตัวพิมพ์ใหญ่ ตัวเลข หรือ _ และขึ้นต้นด้วยตัวอักษร (UPPER_SNAKE_CASE) เช่น PAYMENT_MODE");
            if (!CodePattern().IsMatch(value.Code))
                return ReferenceSaveResult.Fail("รหัสต้องเป็นตัวอักษรอังกฤษ ตัวเลข หรือ _ - . และไม่มีช่องว่าง (ไม่เกิน 50 ตัว)");
            if (value.NameTH.Length == 0)
                return ReferenceSaveResult.Fail("กรุณาระบุชื่อภาษาไทย");

            try
            {
                ReferenceValue? existing = null;
                if (value.Id > 0)
                {
                    existing = await _context.ReferenceValues.FirstOrDefaultAsync(rv => rv.Id == value.Id);
                    if (existing is null)
                        return ReferenceSaveResult.Fail("ไม่พบรายการที่ต้องการแก้ไข (อาจถูกลบไปแล้ว)");
                    // ค่าระดับระบบ: โค้ดอ้างอิงหมวด/รหัส จึงห้ามเปลี่ยน — แก้ได้เฉพาะชื่อ รายละเอียด และลำดับ
                    if (existing.IsSystem && (existing.Category != value.Category || existing.Code != value.Code))
                    {
                        _logger.LogWarning("Attempted to change code of system reference value: {Category}/{Code}",
                            existing.Category, existing.Code);
                        return ReferenceSaveResult.Fail("ค่าระดับระบบเปลี่ยนหมวด/รหัสไม่ได้ (แก้ได้เฉพาะชื่อ รายละเอียด และลำดับ)");
                    }
                }

                // ตรวจสอบ unique constraint (Category + Code)
                var duplicate = await _context.ReferenceValues
                    .AnyAsync(rv => rv.Category == value.Category &&
                                   rv.Code == value.Code &&
                                   rv.Id != value.Id &&
                                   rv.IsActive);

                if (duplicate)
                {
                    _logger.LogWarning("Duplicate reference value: {Category}/{Code}", value.Category, value.Code);
                    return ReferenceSaveResult.Fail($"รหัส {value.Code} มีอยู่แล้วในหมวด {value.Category}");
                }

                var oldCategory = existing?.Category;
                if (existing is null)
                {
                    // รายการใหม่ต่อท้ายหมวด (ถ้าไม่ได้ระบุลำดับ)
                    if (value.DisplayOrder <= 0)
                    {
                        var max = await _context.ReferenceValues.Where(rv => rv.Category == value.Category)
                            .MaxAsync(rv => (int?)rv.DisplayOrder) ?? 0;
                        value.DisplayOrder = max + DISPLAY_ORDER_STEP;
                    }
                    value.IsSystem = false; // ค่าระดับระบบสร้างจาก seed/migration เท่านั้น
                    _context.ReferenceValues.Add(value);
                }
                else
                {
                    // คัดลอกเฉพาะช่องที่แก้ได้ — CreatedAt/CreatedBy/IsSystem คงเดิม
                    existing.Category = value.Category;
                    existing.Code = value.Code;
                    existing.NameTH = value.NameTH;
                    existing.NameEN = value.NameEN;
                    existing.Description = value.Description;
                    if (value.DisplayOrder > 0) existing.DisplayOrder = value.DisplayOrder;
                }

                await _context.SaveChangesAsync();
                InvalidateCache(value.Category);
                if (oldCategory is not null && oldCategory != value.Category) InvalidateCache(oldCategory);

                return ReferenceSaveResult.SUCCESS;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving reference value: {Category}/{Code}", value.Category, value.Code);
                return ReferenceSaveResult.Fail("บันทึกไม่สำเร็จ — ตรวจการเชื่อมต่อฐาน QSHC_Financial");
            }
        }

        /// <summary>หมวดทั้งหมดพร้อมจำนวนรายการ</summary>
        public async Task<List<ReferenceCategorySummary>> GetCategoriesAsync()
        {
            var rows = await _context.ReferenceValues
                .AsNoTracking()
                .Where(rv => rv.IsActive)
                .GroupBy(rv => rv.Category)
                .Select(g => new { Category = g.Key, Count = g.Count(), SystemCount = g.Count(rv => rv.IsSystem) })
                .ToListAsync();
            return rows.Select(r => new ReferenceCategorySummary(r.Category, r.Count, r.SystemCount))
                .OrderBy(c => c.Category, StringComparer.Ordinal).ToList();
        }

        /// <summary>อ่าน 1 รายการ</summary>
        public async Task<ReferenceValue?> GetByIdAsync(int id)
        {
            return await _context.ReferenceValues.AsNoTracking().FirstOrDefaultAsync(rv => rv.Id == id && rv.IsActive);
        }

        /// <summary>
        /// จัดลำดับใหม่ (ลากเรียงในหน้าจัดการ) — ลำดับแสดงผลเปลี่ยนได้ทั้งค่าทั่วไปและค่าระดับระบบ
        /// (IsSystem ป้องกันการแก้รหัส/ชื่อ และการลบ ไม่ใช่ลำดับการแสดง)
        /// </summary>
        public async Task<bool> ReorderAsync(string category, IReadOnlyList<int> orderedIds)
        {
            try
            {
                var values = await _context.ReferenceValues.Where(rv => rv.Category == category && rv.IsActive).ToListAsync();
                var byId = values.ToDictionary(v => v.Id);
                if (values.Count == 0 || orderedIds.Count != values.Count || orderedIds.Distinct().Count() != orderedIds.Count
                    || !orderedIds.All(byId.ContainsKey))
                {
                    _logger.LogWarning("Reorder rejected for {Category}: ids do not match the category", category);
                    return false;
                }

                for (var i = 0; i < orderedIds.Count; i++)
                {
                    var order = (i + 1) * DISPLAY_ORDER_STEP;
                    var value = byId[orderedIds[i]];
                    if (value.DisplayOrder != order) value.DisplayOrder = order;
                }

                await _context.SaveChangesAsync();
                InvalidateCache(category);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reordering reference values: {Category}", category);
                return false;
            }
        }

        [GeneratedRegex("^[A-Z][A-Z0-9_]{0,99}$")]
        private static partial Regex CategoryPattern();

        [GeneratedRegex(@"^[A-Za-z0-9_.\-]{1,50}$")]
        private static partial Regex CodePattern();

        /// <summary>
        /// ลบ Reference Value (Soft Delete) - ห้ามลบ System records
        /// </summary>
        public async Task<bool> DeleteAsync(int id)
        {
            try
            {
                var value = await _context.ReferenceValues.FindAsync(id);
                if (value == null)
                {
                    return false;
                }

                if (value.IsSystem)
                {
                    _logger.LogWarning("Attempted to delete system reference value: {Category}/{Code}",
                        value.Category, value.Code);
                    return false;
                }

                value.IsActive = false;
                await _context.SaveChangesAsync();
                InvalidateCache(value.Category);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting reference value: {Id}", id);
                return false;
            }
        }

        /// <summary>
        /// Invalidate cache สำหรับ Category ที่ระบุ
        /// </summary>
        public void InvalidateCache(string category)
        {
            var cacheKey = $"RefValue_{category}";
            _cache.Remove(cacheKey);
            _logger.LogInformation("Cache invalidated for category: {Category}", category);
        }
    }
}
