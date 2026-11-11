using Financial.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Financial.Navigation
{
    /// <summary>
    /// คัดเมนูตามสิทธิ์ของผู้ใช้ (ใช้ policy เดียวกับ controller จึงไม่มีเมนูที่กดแล้วเจอ Access Denied)
    /// และตามการตั้งค่าเปิด/ปิดกลุ่มเมนูของผู้ดูแลระบบ (ซ่อนจากเมนูเท่านั้น สิทธิ์เข้าหน้ายังเป็นไปตาม role)
    /// </summary>
    public static class AppMenuFilter
    {
        public static async Task<IReadOnlyList<AppMenu.Group>> VisibleGroupsAsync(
            IAuthorizationService authorization, ClaimsPrincipal user, IAppSettingsStore settingsStore, CancellationToken cancellationToken = default)
        {
            var setting = await settingsStore.GetAsync(cancellationToken);
            var groups = new List<AppMenu.Group>();

            foreach (var group in AppMenu.Groups)
            {
                if (IsHidden(group, setting)) continue;

                var items = new List<AppMenu.Item>();
                foreach (var item in group.Items)
                {
                    if (item.Policy is null || (await authorization.AuthorizeAsync(user, item.Policy)).Succeeded)
                    {
                        items.Add(item);
                    }
                }

                if (items.Count > 0)
                {
                    groups.Add(group with { Items = items });
                }
            }

            return groups;
        }

        public static bool IsHidden(AppMenu.Group group, MenuVisibilitySettings setting)
            => group.CanHide && setting.HiddenMenuGroups.Contains(group.Key, StringComparer.OrdinalIgnoreCase);
    }
}
