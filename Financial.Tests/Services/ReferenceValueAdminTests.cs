using Financial.Data;
using Financial.Models;
using Financial.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Financial.Tests.Services
{
    /// <summary>หน้าจัดการค่าอ้างอิง: บันทึก/แก้ไข/ลบ/จัดลำดับ ผ่าน ReferenceValueService</summary>
    public sealed class ReferenceValueAdminTests : IDisposable
    {
        private readonly ApplicationDbContext _db;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly ReferenceValueService _service;
        private static readonly DateTime CREATED_AT = new(2026, 1, 2, 8, 0, 0);

        public ReferenceValueAdminTests()
        {
            _db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            _db.ReferenceValues.AddRange(
                new ReferenceValue { Id = 1, Category = "PAY_MODE", Code = "CASH", NameTH = "เงินสด", DisplayOrder = 10, IsSystem = true },
                new ReferenceValue { Id = 2, Category = "PAY_MODE", Code = "QR", NameTH = "QR Code", DisplayOrder = 20 },
                new ReferenceValue { Id = 3, Category = "PAY_MODE", Code = "CARD", NameTH = "บัตร", DisplayOrder = 30 },
                new ReferenceValue { Id = 4, Category = "GENDER", Code = "M", NameTH = "ชาย", DisplayOrder = 1 },
                new ReferenceValue { Id = 5, Category = "PAY_MODE", Code = "OLD", NameTH = "เลิกใช้", DisplayOrder = 40, IsActive = false });
            _db.SaveChanges();
            // จำลองแถวที่สร้างไว้แล้วโดยผู้ใช้อื่น
            foreach (var v in _db.ReferenceValues.IgnoreQueryFilters()) { v.CreatedAt = CREATED_AT; v.CreatedBy = "creator"; }
            _db.SaveChanges();
            _db.ChangeTracker.Clear();
            _service = new ReferenceValueService(_db, _cache, NullLogger<ReferenceValueService>.Instance);
        }

        public void Dispose()
        {
            _db.Dispose();
            _cache.Dispose();
        }

        [Fact]
        public async Task GetCategories_CountsOnlyActiveValues()
        {
            var categories = await _service.GetCategoriesAsync();

            categories.Should().Equal(
                new ReferenceCategorySummary("GENDER", 1, 0),
                new ReferenceCategorySummary("PAY_MODE", 3, 1));
        }

        [Fact]
        public async Task Create_NormalizesCategory_AndAppendsToEnd()
        {
            var result = await _service.TrySaveAsync(new ReferenceValue { Category = " pay_mode ", Code = "TRNFR", NameTH = " โอนเงิน ", IsSystem = true });

            result.Ok.Should().BeTrue();
            var saved = await _db.ReferenceValues.AsNoTracking().SingleAsync(v => v.Code == "TRNFR");
            saved.Category.Should().Be("PAY_MODE");
            saved.NameTH.Should().Be("โอนเงิน");
            saved.DisplayOrder.Should().Be(40, "ต่อท้ายลำดับสูงสุดของรายการที่ใช้งานอยู่ในหมวด + 10");
            saved.IsSystem.Should().BeFalse("สร้างค่าระดับระบบจากหน้าจอไม่ได้");
        }

        [Theory]
        [InlineData("pay mode", "X", "ชื่อ", "UPPER_SNAKE_CASE")]
        [InlineData("PAY_MODE", "A B", "ชื่อ", "ไม่มีช่องว่าง")]
        [InlineData("PAY_MODE", "X", " ", "ชื่อภาษาไทย")]
        [InlineData("PAY_MODE", "QR", "ซ้ำ", "มีอยู่แล้ว")]
        public async Task Save_InvalidInput_ReturnsThaiReason(string category, string code, string name, string expected)
        {
            var result = await _service.TrySaveAsync(new ReferenceValue { Category = category, Code = code, NameTH = name });

            result.Ok.Should().BeFalse();
            result.Error.Should().Contain(expected);
        }

        [Fact]
        public async Task Update_KeepsAuditCreatedFields_AndIsSystem()
        {
            // ฟอร์มส่งมาเฉพาะช่องที่แก้ได้ (CreatedAt/CreatedBy เป็นค่าว่าง)
            var result = await _service.TrySaveAsync(new ReferenceValue { Id = 2, Category = "PAY_MODE", Code = "QRCODE", NameTH = "คิวอาร์", DisplayOrder = 20 });

            result.Ok.Should().BeTrue();
            var saved = await _db.ReferenceValues.AsNoTracking().SingleAsync(v => v.Id == 2);
            saved.Code.Should().Be("QRCODE");
            saved.NameTH.Should().Be("คิวอาร์");
            saved.CreatedAt.Should().Be(CREATED_AT);
            saved.CreatedBy.Should().Be("creator");
        }

        [Fact]
        public async Task SystemValue_NamesEditable_ButCodeLockedAndNotDeletable()
        {
            (await _service.TrySaveAsync(new ReferenceValue { Id = 1, Category = "PAY_MODE", Code = "CASH2", NameTH = "แก้" })).Error.Should().Contain("เปลี่ยนหมวด/รหัสไม่ได้");
            (await _service.DeleteAsync(1)).Should().BeFalse();

            (await _service.TrySaveAsync(new ReferenceValue { Id = 1, Category = "PAY_MODE", Code = "CASH", NameTH = "เงินสด (หน้าเคาน์เตอร์)", DisplayOrder = 10 })).Ok.Should().BeTrue();
            var saved = await _db.ReferenceValues.AsNoTracking().SingleAsync(v => v.Id == 1);
            saved.NameTH.Should().Be("เงินสด (หน้าเคาน์เตอร์)");
            saved.IsSystem.Should().BeTrue();
        }

        [Fact]
        public async Task Reorder_SetsStepOrder_AndRefreshesCache()
        {
            (await _service.GetByCategoryAsync("PAY_MODE")).Select(v => v.Code).Should().Equal("CASH", "QR", "CARD");

            (await _service.ReorderAsync("PAY_MODE", [3, 1, 2])).Should().BeTrue();

            var values = await _service.GetByCategoryAsync("PAY_MODE");
            values.Select(v => v.Code).Should().Equal(["CARD", "CASH", "QR"], "cache ของหมวดต้องถูกล้างหลังจัดลำดับ");
            values.Select(v => v.DisplayOrder).Should().Equal(10, 20, 30);
        }

        [Theory]
        [InlineData(new[] { 3, 1 })]          // ไม่ครบ
        [InlineData(new[] { 3, 1, 2, 4 })]    // ปนหมวดอื่น
        [InlineData(new[] { 3, 3, 2 })]       // ซ้ำ
        [InlineData(new[] { 3, 1, 5 })]       // แถวที่ลบแล้ว
        public async Task Reorder_RejectsIdsThatDoNotMatchCategory(int[] ids)
        {
            (await _service.ReorderAsync("PAY_MODE", ids)).Should().BeFalse();

            (await _db.ReferenceValues.AsNoTracking().Where(v => v.Category == "PAY_MODE").OrderBy(v => v.Id).Select(v => v.DisplayOrder).ToListAsync())
                .Should().Equal(10, 20, 30);
        }
    }
}
