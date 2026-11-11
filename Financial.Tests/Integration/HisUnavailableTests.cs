using FluentAssertions;
using Financial.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Financial.Services.Dashboard;
using System.Text.Json;

namespace Financial.Tests.Integration
{
    /// <summary>
    /// Dashboard:Source = His แต่เชื่อมต่อฐานไม่ได้ → ต้องไม่ล่ม และแจ้งเตือนรายส่วน
    /// (ชี้ไปยังพอร์ตที่ไม่มีอยู่ — ไม่แตะฐานข้อมูลจริง)
    /// </summary>
    public class HisUnavailableTests : IClassFixture<HisUnavailableTests.Factory>
    {
        public sealed class Factory : WebApplicationFactory<Program>
        {
            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.UseTestDefaults("His");
            }
        }

        private readonly Factory _factory;

        public HisUnavailableTests(Factory factory) => _factory = factory;

        [Fact]
        public async Task HisService_DatabaseDown_ReturnsEmptyDashboardWithWarnings()
        {
            using var scope = _factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IFinanceDashboardService>();

            service.Should().BeOfType<HisFinanceDashboardService>();

            var dashboard = await service.GetAsync();

            dashboard.IsSample.Should().BeFalse();
            dashboard.Warnings.Should().HaveCount(6, "ทุกส่วนล้มเหลวแยกกัน และแจ้งเตือนครบทุกส่วน");
            dashboard.RevenueMonth.Total.Should().Be(0);
            dashboard.DailyRevenue.Should().HaveCount(30);
            JsonSerializer.Serialize(dashboard).Should().Contain("Warnings");
        }
    }
}
