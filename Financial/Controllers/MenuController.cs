using Microsoft.AspNetCore.Mvc;

namespace Financial.Controllers
{
    public class MenuController : Controller
    {
        public class MenuItem
        {
            public required string Name { get; set; }
            public required string Url { get; set; }
            public required string Icon { get; set; }
            public required string Controller { get; set; }
            public required string Action { get; set; }
        }

        public IActionResult GetMainMenu()
        {
            var menus = new List<MenuItem>
            {
                new MenuItem 
                { 
                    Name = "Overview", 
                    Url = "/", 
                    Icon = "📊",
                    Controller = "Home",
                    Action = "Index"
                },
                new MenuItem 
                { 
                    Name = "Activity", 
                    Url = "#", 
                    Icon = "📈",
                    Controller = "",
                    Action = ""
                },
                new MenuItem 
                { 
                    Name = "Manage", 
                    Url = "#", 
                    Icon = "⚙️",
                    Controller = "",
                    Action = ""
                },
                new MenuItem 
                { 
                    Name = "Program", 
                    Url = "#", 
                    Icon = "📋",
                    Controller = "",
                    Action = ""
                },
                new MenuItem 
                { 
                    Name = "Account", 
                    Url = "/InterfaceSystem", 
                    Icon = "👤",
                    Controller = "InterfaceSystem",
                    Action = "Index"
                },
                new MenuItem 
                { 
                    Name = "Reports", 
                    Url = "#", 
                    Icon = "📑",
                    Controller = "",
                    Action = ""
                }
            };

            return Json(menus);
        }

        public IActionResult GetSidebarMenu()
        {
            var menus = new List<MenuItem>
            {
                new MenuItem 
                { 
                    Name = "Home", 
                    Url = "/", 
                    Icon = "🏠",
                    Controller = "Home",
                    Action = "Index"
                },
                new MenuItem 
                { 
                    Name = "Discharges", 
                    Url = "/Discharges", 
                    Icon = "🏥",
                    Controller = "Discharges",
                    Action = "Index"
                },
                new MenuItem 
                { 
                    Name = "Interface System", 
                    Url = "/InterfaceSystem", 
                    Icon = "🔗",
                    Controller = "InterfaceSystem",
                    Action = "Index"
                },
                new MenuItem 
                { 
                    Name = "Reports", 
                    Url = "#", 
                    Icon = "📊",
                    Controller = "",
                    Action = ""
                }
            };

            return Json(menus);
        }
    }
}
