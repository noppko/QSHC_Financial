using Financial.Services;
using Financial.Services.Dashboard;
using FluentAssertions;

namespace Financial.Tests.Services
{
    public class UploadPathGuardTests
    {
        private static readonly string Root = Path.Combine(Path.GetTempPath(), "edc-root");

        [Fact]
        public void TryResolve_PlainFileName_ResolvesInsideRoot()
        {
            UploadPathGuard.TryResolve(Root, "claim_001.pdf", out var full).Should().BeTrue();
            full.Should().Be(Path.Combine(Path.GetFullPath(Root), "claim_001.pdf"));
        }

        [Theory]
        [InlineData("..\\secret.pdf")]
        [InlineData("../secret.pdf")]
        [InlineData("sub\\a.pdf")]
        [InlineData("C:\\Windows\\win.ini")]
        [InlineData("\\\\10.0.0.1\\share\\x.pdf")]
        [InlineData("..")]
        [InlineData("")]
        [InlineData(null)]
        public void TryResolve_PathTraversalOrInvalid_IsRejected(string? fileName)
        {
            UploadPathGuard.TryResolve(Root, fileName, out var full).Should().BeFalse();
            full.Should().BeEmpty();
        }

        [Fact]
        public void TryResolve_NoRootConfigured_IsRejected()
        {
            UploadPathGuard.TryResolve("", "a.pdf", out _).Should().BeFalse();
        }

        [Theory]
        [InlineData("a.pdf", true)]
        [InlineData("A.PDF", true)]
        [InlineData("a.exe", false)]
        [InlineData("noext", false)]
        public void FileUploadOptions_IsAllowedExtension(string name, bool expected)
        {
            new FileUploadOptions { AllowedExtensions = [".pdf"] }.IsAllowedExtension(name).Should().Be(expected);
        }
    }

    public class FinanceDashboardTests
    {
        [Theory]
        [InlineData(123456789L, "#6789")]
        [InlineData(42L, "#0042")]
        [InlineData(10000L, "#0000")]
        public void Reference_ShowsOnlyLastFourDigits(long uid, string expected)
        {
            DashboardText.Reference(uid).Should().Be(expected);
        }

        [Theory]
        [InlineData("CASHH", "เงินสด")]
        [InlineData("QRCODE", "QR Code")]
        [InlineData("?", "ไม่ระบุ")]
        [InlineData("NEWMODE", "NEWMODE")]
        public void PaymentModeName_MapsKnownCodes(string code, string expected)
        {
            DashboardText.PaymentModeName(code).Should().Be(expected);
        }

        [Fact]
        public async Task SampleService_ProducesConsistentDashboard()
        {
            var service = new SampleFinanceDashboardService(TimeProvider.System);

            var d = await service.GetAsync();

            d.IsSample.Should().BeTrue("ต้องแสดงป้าย 'ข้อมูลตัวอย่าง' บนหน้าจอ");
            d.DailyRevenue.Should().HaveCount(30);
            d.DailyRevenue.Should().BeInAscendingOrder(x => x.Date);
            d.RevenueMonth.Total.Should().Be(d.CashBilledMonth.Total + d.CreditBilledMonth.Total);
            d.PendingPayments.Should().Contain(p => p.Type == "O").And.Contain(p => p.Type == "I");
            d.PendingPayments.Should().OnlyContain(p => (p.Type == "O" || p.Type == "I") && p.Amount > 0 && p.Reference.StartsWith('#'));
        }

        [Fact]
        public async Task SampleService_SameDay_ReturnsSameNumbers()
        {
            var service = new SampleFinanceDashboardService(TimeProvider.System);

            var a = await service.GetAsync();
            var b = await service.GetAsync();

            a.PendingPayments.Select(p => p.Amount).Should().Equal(b.PendingPayments.Select(p => p.Amount), "ตัวเลขไม่ควรกระโดดทุกครั้งที่ refresh");
        }
    }
}
