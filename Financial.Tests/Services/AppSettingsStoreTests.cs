using Financial.Data;
using Financial.Models;
using Financial.Navigation;
using Financial.Services;
using Financial.Services.Authorization;
using Financial.Services.Settings;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Financial.Tests.Services
{
    /// <summary>
    /// การตั้งค่าเมนูเก็บที่ตาราง AppSettings (QSHC_Financial) — ทดสอบด้วย EF InMemory
    /// </summary>
    public class AppSettingsStoreTests
    {
        private readonly ServiceProvider _services;

        public AppSettingsStoreTests()
        {
            var dbName = Guid.NewGuid().ToString("N");
            _services = new ServiceCollection()
                .AddLogging()
                .AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName))
                .AddAuthorization(o => AppPolicies.Configure(o, new RoleOptions()))
                .BuildServiceProvider();
        }

        private DbAppSettingsStore CreateStore() => new(_services.GetRequiredService<IServiceScopeFactory>(), NullLogger<DbAppSettingsStore>.Instance);

        [Fact]
        public async Task Get_NoRow_ReturnsDefaultsShowingAllMenus()
        {
            var setting = await CreateStore().GetAsync();
            setting.HiddenMenuGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Save_PersistsAsAppSettingRow_WithAuditFields()
        {
            await CreateStore().SaveAsync(new MenuVisibilitySettings { HiddenMenuGroups = ["interface"] });

            var reloaded = await CreateStore().GetAsync(); // เหมือน restart แอป
            reloaded.HiddenMenuGroups.Should().Equal("interface");
            reloaded.UpdatedBy.Should().Be("System", "ไม่มีผู้ใช้ใน HttpContext ระหว่าง test");
            reloaded.UpdatedAt.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMinutes(1), "ต้องเป็นเวลาไทย (DateTime.Now)");

            using var scope = _services.CreateScope();
            var row = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AppSettings.SingleAsync();
            row.Key.Should().Be(AppSettingKeys.MENU_HIDDEN_GROUPS);
            row.Value.Should().Be("[\"interface\"]");
            row.IsActive.Should().BeTrue();
        }

        [Fact]
        public async Task Save_Twice_UpdatesSameRow()
        {
            var store = CreateStore();
            await store.SaveAsync(new MenuVisibilitySettings { HiddenMenuGroups = ["interface"] });
            await store.SaveAsync(new MenuVisibilitySettings { HiddenMenuGroups = [] });

            using var scope = _services.CreateScope();
            var rows = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AppSettings.ToListAsync();
            rows.Should().ContainSingle().Which.Value.Should().Be("[]");
            rows[0].UpdatedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task Get_InvalidJson_FallsBackToDefaults()
        {
            using (var scope = _services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.AppSettings.Add(new AppSetting { Key = AppSettingKeys.MENU_HIDDEN_GROUPS, Value = "{ not json" });
                await db.SaveChangesAsync();
            }

            (await CreateStore().GetAsync()).HiddenMenuGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task Get_DatabaseUnavailable_ShowsAllMenusInsteadOfFailing()
        {
            var broken = new ServiceCollection().AddLogging()
                .AddDbContext<ApplicationDbContext>(o => o.UseSqlServer("Server=127.0.0.1,1;Database=none;User ID=x;Password=x;Connect Timeout=1;ConnectRetryCount=0"))
                .BuildServiceProvider();
            var store = new DbAppSettingsStore(broken.GetRequiredService<IServiceScopeFactory>(), NullLogger<DbAppSettingsStore>.Instance);

            (await store.GetAsync()).HiddenMenuGroups.Should().BeEmpty();
        }

        [Fact]
        public async Task MenuFilter_HidesConfiguredGroups_ButNeverAdminGroup()
        {
            var store = CreateStore();
            await store.SaveAsync(new MenuVisibilitySettings { HiddenMenuGroups = ["discharge", "ADMIN", "other"] });
            var admin = UserClaimsFactory.Create(new Financial.DTOs.Account.AuthData { LoginName = "a", Roles = ["Admin"] });

            var groups = await AppMenuFilter.VisibleGroupsAsync(_services.GetRequiredService<IAuthorizationService>(), admin, store);

            groups.Select(g => g.Key).Should().Equal("overview", "reports", "interface", "admin");
        }

        [Fact]
        public void AllMenuGroups_HaveUniqueStableKeys()
        {
            AppMenu.Groups.Select(g => g.Key).Should().OnlyHaveUniqueItems().And.NotContain(string.Empty);
            AppMenu.Groups.Single(g => g.Key == "admin").CanHide.Should().BeFalse();
        }
    }
}
