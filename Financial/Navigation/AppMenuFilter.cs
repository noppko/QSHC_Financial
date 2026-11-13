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
            IAuthorizationService authorization, ClaimsPrincipal user, IAppSettingsStore settingsStore, CancellationToken cancellationToken = default,
            IReadOnlyList<AppMenu.Group>? source = null)
        {
            var setting = await settingsStore.GetAsync(cancellationToken);
            var groups = new List<AppMenu.Group>();

            foreach (var group in source ?? AppMenu.Groups)
            {
                if (IsHidden(group, setting)) continue;

                var items = new List<AppMenu.Item>();
                foreach (var item in group.Items)
                {
                    if (!await CanSeeAsync(authorization, user, item)) continue;

                    if (item.HasChildren)
                    {
                        // หัวข้อระดับ 1 แสดงเฉพาะลูกที่มีสิทธิ์ — ไม่มีลูกที่เข้าได้เลยก็ซ่อนหัวข้อ
                        var children = new List<AppMenu.Item>();
                        foreach (var child in item.Children!)
                        {
                            if (await CanSeeAsync(authorization, user, child)) children.Add(child);
                        }
                        if (children.Count > 0) items.Add(item with { Children = children });
                    }
                    else
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

        private static async Task<bool> CanSeeAsync(IAuthorizationService authorization, ClaimsPrincipal user, AppMenu.Item item)
            => item.Policy is null || (await authorization.AuthorizeAsync(user, item.Policy)).Succeeded;

        public static bool IsHidden(AppMenu.Group group, MenuVisibilitySettings setting)
            => group.CanHide && setting.HiddenMenuGroups.Contains(group.Key, StringComparer.OrdinalIgnoreCase);
    }
}
