using Financial.Data;
using Financial.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Financial.Services.Settings
{
    /// <summary>
    /// การตั้งค่าการแสดงกลุ่มเมนู (อ่าน/เขียนจากตาราง AppSettings)
    /// </summary>
    public sealed record MenuVisibilitySettings
    {
        /// <summary>Key ของกลุ่มเมนูที่ซ่อน (AppMenu.Group.Key)</summary>
        public IReadOnlyList<string> HiddenMenuGroups { get; init; } = [];

        public DateTime? UpdatedAt { get; init; }
        public string? UpdatedBy { get; init; }
    }

    public interface IAppSettingsStore
    {
        Task<MenuVisibilitySettings> GetAsync(CancellationToken cancellationToken = default);
        Task SaveAsync(MenuVisibilitySettings settings, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// เก็บที่ตาราง AppSettings ในฐาน QSHC_Financial (DefaultConnection) — แยกจาก HIS
    /// - อ่านครั้งเดียวแล้วเก็บในหน่วยความจำ (ทุก request ใช้ร่วมกัน) และล้างเมื่อบันทึก
    /// - ถ้าฐานใช้งานไม่ได้ จะแสดงเมนูทั้งหมด (ค่าเริ่มต้น) และลองอ่านใหม่ทุก 1 นาที — เมนูไม่หายเพราะฐานล่ม
    /// </summary>
    public sealed class DbAppSettingsStore : IAppSettingsStore
    {
        private static readonly TimeSpan RetryAfterFailure = TimeSpan.FromMinutes(1);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DbAppSettingsStore> _logger;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private MenuVisibilitySettings? _current;
        private DateTime _retryAt = DateTime.MinValue;

        public DbAppSettingsStore(IServiceScopeFactory scopeFactory, ILogger<DbAppSettingsStore> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task<MenuVisibilitySettings> GetAsync(CancellationToken cancellationToken = default)
        {
            if (_current is not null) return _current;
            if (DateTime.Now < _retryAt) return new MenuVisibilitySettings();

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_current is not null) return _current;

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == AppSettingKeys.MENU_HIDDEN_GROUPS, cancellationToken);

                _current = new MenuVisibilitySettings
                {
                    HiddenMenuGroups = ParseList(row?.Value),
                    UpdatedAt = row?.UpdatedAt ?? row?.CreatedAt,
                    UpdatedBy = row?.UpdatedBy ?? row?.CreatedBy,
                };
                return _current;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Cannot read AppSettings from QSHC_Financial — showing all menus");
                _retryAt = DateTime.Now.Add(RetryAfterFailure);
                return new MenuVisibilitySettings();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SaveAsync(MenuVisibilitySettings settings, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var row = await db.AppSettings.FirstOrDefaultAsync(s => s.Key == AppSettingKeys.MENU_HIDDEN_GROUPS, cancellationToken);

                if (row is null)
                {
                    row = new AppSetting { Key = AppSettingKeys.MENU_HIDDEN_GROUPS, Description = "กลุ่มเมนูที่ซ่อน (JSON array ของ AppMenu.Group.Key)" };
                    db.AppSettings.Add(row);
                }

                row.Value = JsonSerializer.Serialize(settings.HiddenMenuGroups);
                await db.SaveChangesAsync(cancellationToken); // CreatedBy/UpdatedBy/UpdatedAt ตั้งอัตโนมัติจากผู้ใช้ที่ Login

                _current = new MenuVisibilitySettings
                {
                    HiddenMenuGroups = settings.HiddenMenuGroups,
                    UpdatedAt = row.UpdatedAt ?? row.CreatedAt,
                    UpdatedBy = row.UpdatedBy ?? row.CreatedBy,
                };
                _retryAt = DateTime.MinValue;
            }
            finally
            {
                _lock.Release();
            }
        }

        private static IReadOnlyList<string> ParseList(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return [];
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? [];
            }
            catch (JsonException)
            {
                return [];
            }
        }
    }
}
