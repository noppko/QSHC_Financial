using Financial.Data;
using Financial.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Financial.Services
{
    /// <summary>
    /// Service สำหรับจัดการ Reference Values พร้อม caching
    /// </summary>
    public class ReferenceValueService : IReferenceValueService
    {
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
        /// เพิ่มหรืออัปเดต Reference Value
        /// </summary>
        public async Task<bool> SaveAsync(ReferenceValue value)
        {
            try
            {
                // ตรวจสอบ IsSystem - ถ้าเป็น System record ห้ามแก้ไข
                if (value.Id > 0)
                {
                    var existing = await _context.ReferenceValues.FindAsync(value.Id);
                    if (existing?.IsSystem == true)
                    {
                        _logger.LogWarning("Attempted to modify system reference value: {Category}/{Code}",
                            existing.Category, existing.Code);
                        return false;
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
                    return false;
                }

                if (value.Id == 0)
                {
                    _context.ReferenceValues.Add(value);
                }
                else
                {
                    _context.ReferenceValues.Update(value);
                }

                await _context.SaveChangesAsync();
                InvalidateCache(value.Category);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving reference value: {Category}/{Code}", value.Category, value.Code);
                return false;
            }
        }

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
