using Financial.Models;
using FluentAssertions;

namespace Financial.Tests.Models
{
    public class DateRangeTests
    {
        private static readonly DateOnly Today = new(2026, 10, 22);

        [Fact]
        public void TryParse_NoInput_DefaultsToYesterday()
        {
            DateRange.TryParse(null, null, Today, out var range, out var error).Should().BeTrue();

            error.Should().BeNull();
            range.Start.Should().Be(new DateOnly(2026, 10, 21));
            range.End.Should().Be(new DateOnly(2026, 10, 21));
        }

        [Fact]
        public void TryParse_OnlyStart_EndDefaultsToStart()
        {
            DateRange.TryParse("2026-10-01", "", Today, out var range, out _).Should().BeTrue();

            range.Should().Be(new DateRange(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 1)));
        }

        [Fact]
        public void TryParse_ValidRange_ProducesSqlBoundariesInGregorian()
        {
            DateRange.TryParse("2026-10-01", "2026-10-15", Today, out var range, out _).Should().BeTrue();

            range.Days.Should().Be(15);
            range.SqlFrom.Should().Be("2026-10-01 00:00:00.000");
            // .997 เพราะ SQL Server datetime ปัด .999 ขึ้นเป็นวันถัดไป
            range.SqlTo.Should().Be("2026-10-15 23:59:59.997");
        }

        [Theory]
        [InlineData("2026/10/01", "2026-10-02")]
        [InlineData("01-10-2026", null)]
        [InlineData("2026-10-01'; DROP TABLE x;--", null)]
        [InlineData("2026-02-30", null)]
        public void TryParse_InvalidFormat_Fails(string start, string? end)
        {
            DateRange.TryParse(start, end, Today, out _, out var error).Should().BeFalse();
            error.Should().Be("รูปแบบวันที่ไม่ถูกต้อง");
        }

        [Fact]
        public void TryParse_EndBeforeStart_Fails()
        {
            DateRange.TryParse("2026-10-10", "2026-10-01", Today, out _, out var error).Should().BeFalse();
            error.Should().Contain("ไม่น้อยกว่า");
        }

        [Fact]
        public void TryParse_RangeTooLong_Fails()
        {
            DateRange.TryParse("2024-01-01", "2026-01-01", Today, out _, out var error).Should().BeFalse();
            error.Should().Contain(DateRange.MAX_DAYS.ToString());
        }
    }
}
