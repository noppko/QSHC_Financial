using Microsoft.AspNetCore.Mvc;

namespace Financial.Components
{
    public class SidebarMenuViewComponent : ViewComponent
    {
        public class MenuItem
        {
            public required string Name { get; set; }
            public required string Url { get; set; }
            public required string Icon { get; set; }
            public required string Controller { get; set; }
            public required string Action { get; set; }
        }

        public IViewComponentResult Invoke()
        {
            var menus = new List<MenuItem>
            {
                new MenuItem { Name = "Home", Url = "/", Icon = "🏠", Controller = "Home", Action = "Index" },
                new MenuItem { Name = "All Pages", Url = "/Home/SiteMap", Icon = "🗺️", Controller = "Home", Action = "SiteMap" },
                new MenuItem { Name = "Privacy Policy", Url = "/Home/Privacy", Icon = "🔒", Controller = "Home", Action = "Privacy" },
                new MenuItem { Name = "Discharges (IPD/OPD)", Url = "/Discharges", Icon = "🏥", Controller = "Discharges", Action = "Index" },
                new MenuItem { Name = "Interface System", Url = "/InterfaceSystem", Icon = "🔗", Controller = "InterfaceSystem", Action = "Index" },
                new MenuItem { Name = "Interface EDC", Url = "/InterfaceEDC", Icon = "💳", Controller = "InterfaceEDC", Action = "Index" }
            };

            return View(menus);
        }
    }
}
