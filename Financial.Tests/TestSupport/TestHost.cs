using Financial.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Financial.Tests.TestSupport
{
    /// <summary>
    /// ตั้งค่าแอปสำหรับ integration test — ห้ามแตะฐานข้อมูลจริงทุกฐาน
    /// - QSHC_Financial (DefaultConnection) → EF InMemory แยกต่อ factory
    /// - HIS / InterfaceSystem → พอร์ตที่ไม่มีอยู่ (ล้มเหลวทันที)
    /// - ปิด migrate อัตโนมัติ และงานประมวลผลรายงานตอนกลางคืน
    /// </summary>
    public static class TestHost
    {
        public const string UNREACHABLE_SQL = "Server=127.0.0.1,1;Database=none;Integrated Security=false;User ID=none;Password=none;Connect Timeout=1;ConnectRetryCount=0";

        public static IWebHostBuilder UseTestDefaults(this IWebHostBuilder builder, string dashboardSource = "Sample")
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Dashboard:Source", dashboardSource);
            builder.UseSetting("Security:LoginAttemptsPerMinute", "1000");
            builder.UseSetting("Database:AutoMigrate", "false");
            builder.UseSetting("Reports:Fiscal:AutoRefreshEnabled", "false");
            builder.UseSetting("ConnectionStrings:DefaultConnection", UNREACHABLE_SQL);
            builder.UseSetting("ConnectionStrings:HealthObject", UNREACHABLE_SQL);
            builder.UseSetting("ConnectionStrings:InterfaceSystem", UNREACHABLE_SQL);

            var databaseName = "financial-test-" + Guid.NewGuid().ToString("N");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(databaseName));
            });
            return builder;
        }

        public static ApplicationDbContext CreateInMemoryDb(string? name = null)
            => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(name ?? Guid.NewGuid().ToString("N")).Options);
    }
}
