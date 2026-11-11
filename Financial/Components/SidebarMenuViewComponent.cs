using Financial.Navigation;
using Financial.Services.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Financial.Components
{
    public class SidebarMenuViewComponent : ViewComponent
    {
        private readonly IAuthorizationService _authorization;
        private readonly IAppSettingsStore _settingsStore;

        public SidebarMenuViewComponent(IAuthorizationService authorization, IAppSettingsStore settingsStore)
        {
            _authorization = authorization;
            _settingsStore = settingsStore;
        }

        public async Task<IViewComponentResult> InvokeAsync()
            => View(await AppMenuFilter.VisibleGroupsAsync(_authorization, HttpContext.User, _settingsStore, HttpContext.RequestAborted));
    }
}
