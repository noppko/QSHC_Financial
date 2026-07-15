using Microsoft.AspNetCore.Mvc;

namespace Financial.Components
{
    public class NavbarMenuViewComponent : ViewComponent
    {
        public class MenuItem
        {
            public required string Name { get; set; }
            public required string Url { get; set; }
            public required string Controller { get; set; }
            public required string Action { get; set; }
        }

        public IViewComponentResult Invoke()
        {
            var menus = new List<MenuItem>
            {
                new MenuItem { Name = "Overview", Url = "/", Controller = "Home", Action = "Index" },
                new MenuItem { Name = "Activity", Url = "#", Controller = "", Action = "" },
                new MenuItem { Name = "Manage", Url = "#", Controller = "", Action = "" },
                new MenuItem { Name = "Program", Url = "#", Controller = "", Action = "" },
                new MenuItem { Name = "Account", Url = "/InterfaceSystem", Controller = "InterfaceSystem", Action = "Index" },
                new MenuItem { Name = "Reports", Url = "#", Controller = "", Action = "" }
            };

            return View(menus);
        }
    }
}
