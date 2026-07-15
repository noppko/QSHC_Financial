using Financial.Data;
using Financial.Models;
using Financial.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Financial.Tests.Services
{
    public class ReferenceValueServiceTests : IDisposable
    {
        private readonly ApplicationDbContext _context;
        private readonly Mock<IMemoryCache> _cacheMock;
        private readonly Mock<ILogger<ReferenceValueService>> _loggerMock;
        private readonly ReferenceValueService _service;

        public ReferenceValueServiceTests()
        {
            // Setup in-memory database
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            _context = new ApplicationDbContext(options);
            _cacheMock = new Mock<IMemoryCache>();
            _loggerMock = new Mock<ILogger<ReferenceValueService>>();

            // Seed test data
            SeedTestData();

            _service = new ReferenceValueService(_context, _cacheMock.Object, _loggerMock.Object);
        }

        private void SeedTestData()
        {
            var referenceValues = new List<ReferenceValue>
            {
                new ReferenceValue
                {
                    Id = 1,
                    Category = "TITLE",
                    Code = "MR",
                    NameTH = "นาย",
                    NameEN = "Mr.",
                    Description = "คำนำหน้าชาย",
                    DisplayOrder = 1,
                    IsActive = true
                },
                new ReferenceValue
                {
                    Id = 2,
                    Category = "TITLE",
                    Code = "MRS",
                    NameTH = "นาง",
                    NameEN = "Mrs.",
                    Description = "คำนำหน้าหญิง (สมรส)",
                    DisplayOrder = 2,
                    IsActive = true
                },
                new ReferenceValue
                {
                    Id = 3,
                    Category = "GENDER",
                    Code = "M",
                    NameTH = "ชาย",
                    NameEN = "Male",
                    Description = "เพศชาย",
                    DisplayOrder = 1,
                    IsActive = true
                },
                new ReferenceValue
                {
                    Id = 4,
                    Category = "GENDER",
                    Code = "F",
                    NameTH = "หญิง",
                    NameEN = "Female",
                    Description = "เพศหญิง",
                    DisplayOrder = 2,
                    IsActive = true
                },
                new ReferenceValue
                {
                    Id = 5,
                    Category = "TITLE",
                    Code = "DR",
                    NameTH = "นายแพทย์",
                    NameEN = "Dr.",
                    Description = "แพทย์",
                    DisplayOrder = 3,
                    IsActive = false // Inactive
                }
            };

            _context.ReferenceValues.AddRange(referenceValues);
            _context.SaveChanges();
        }

        [Fact]
        public async Task GetByCategoryAsync_ValidCategory_ReturnsActiveValues()
        {
            // Arrange
            var category = "TITLE";
            object? cacheEntry = null;
            _cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheEntry)).Returns(false);
            _cacheMock.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

            // Act
            var result = await _service.GetByCategoryAsync(category);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2); // Only active values (MR, MRS - not DR which is inactive)
            result.Should().OnlyContain(r => r.IsActive);
            result.Should().BeInAscendingOrder(r => r.DisplayOrder);
        }

        [Fact]
        public async Task GetByCategoryAsync_InvalidCategory_ReturnsEmptyList()
        {
            // Arrange
            var category = "INVALID_CATEGORY";
            object? cacheEntry = null;
            _cacheMock.Setup(x => x.TryGetValue(It.IsAny<object>(), out cacheEntry)).Returns(false);
            _cacheMock.Setup(x => x.CreateEntry(It.IsAny<object>())).Returns(Mock.Of<ICacheEntry>());

            // Act
            var result = await _service.GetByCategoryAsync(category);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetByCodeAsync_ValidCode_ReturnsReferenceValue()
        {
            // Arrange
            var category = "GENDER";
            var code = "M";

            // Act
            var result = await _service.GetByCodeAsync(category, code);

            // Assert
            result.Should().NotBeNull();
            result!.Code.Should().Be("M");
            result.NameTH.Should().Be("ชาย");
            result.NameEN.Should().Be("Male");
        }

        [Fact]
        public async Task GetByCodeAsync_InvalidCode_ReturnsNull()
        {
            // Arrange
            var category = "GENDER";
            var code = "INVALID";

            // Act
            var result = await _service.GetByCodeAsync(category, code);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetByCodeAsync_InactiveValue_ReturnsNull()
        {
            // Arrange
            var category = "TITLE";
            var code = "DR"; // This is inactive in seed data

            // Act
            var result = await _service.GetByCodeAsync(category, code);

            // Assert
            result.Should().BeNull(); // Should return null for inactive records
        }

        public void Dispose()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }
    }
}
