
using Financial.DTOs.Account;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Financial.Database.QSHC.Contexts;
using Financial.Models.ViewModels;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Financial.Controllers
{
    public class AccountController : Controller
    {
        string connstr = "Server=10.67.67.64;user id=sa;password=Password@HO2021;Database=HealthObject;Trusted_Connection=False;TrustServerCertificate=True; " +
                  " Max Pool Size=400;Connect Timeout=600;";

        private UserManager<IdentityUser> _userManager;
        private RoleManager<IdentityRole> _roleManager;
        private QSHCDb _dbQSHC;
        public IActionResult Login(string returnUrl = null)
        {
            var model = new LoginRequest
            {
                ReturnUrl = returnUrl
            };
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginRequest login)
        {
            if (login == null)
            {
                return RedirectToAction(nameof(Login));
            }

            SqlConnection conn = null;
            SqlDataReader rdr = null;

            conn = new SqlConnection(connstr);

            conn.Open();
            //string sql = "EXEC pRepQSHCVerifyLogin @P_Username='it010' @P_Password='2'";
            //string sql = "EXEC pRepQSHCVerifyLoginRole @P_Username='it010' @P_Password='2'";
            var _Profile = new List<UserProfile>();
            string sql = "EXEC pRepQSHCVerifyLoginRole @P_Username  = '" + login.Username + "',  @P_Password  = '" + login.Password + "'";
            SqlCommand cmdPatient = new SqlCommand(sql, conn);
            cmdPatient.CommandType = CommandType.Text;
            rdr = cmdPatient.ExecuteReader();

            if (rdr.HasRows)
            {
                while (rdr.Read())
                {
                    var Profile = new UserProfile();
                    Profile.Id = Convert.ToInt32(rdr["LoginUID"].ToString());
                    Profile.LoginName = rdr["LoginName"].ToString();
                    Profile.FullName = rdr["FullName"].ToString();
                    Profile.Gender = rdr["Gender"].ToString();
                    Profile.JobPosition = rdr["JobPosition"].ToString();
                    Profile.RoleName = rdr["RoleName"].ToString();
                    _Profile.Add(Profile);
                }
                conn.Close();

                var UserProfile = _Profile.First();

                //----------- Claim 1 role --------------
                //    var _claim = new ClaimsIdentity(new[]
                //{
                //        new Claim(ClaimTypes.Name, UserProfile.FullName),
                //        new Claim("LoginName", UserProfile.LoginName ),
                //        new Claim("FullName", UserProfile.FullName ),
                //        new Claim("Gender", UserProfile.Gender ),
                //        new Claim(ClaimTypes.Role, UserProfile.RoleName ),
                //    }, CookieAuthenticationDefaults.AuthenticationScheme);

                //----------- Claim Multi role --------------
                var claims = new List<Claim>();
                claims.Add(new Claim(ClaimTypes.Name, UserProfile.LoginName));
                claims.Add(new Claim("LoginUID", UserProfile.Id.ToString()));
                claims.Add(new Claim("LoginName", UserProfile.LoginName));
                claims.Add(new Claim("FullName", UserProfile.FullName));
                claims.Add(new Claim("Gender", UserProfile.Gender));
                claims.Add(new Claim("JobPosition", UserProfile.JobPosition));
                foreach (var role in _Profile)
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.RoleName));
                }
                var _claim = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);



                var principal = new ClaimsPrincipal(_claim);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);
                //return RedirectToAction("Index","Home");
                return string.IsNullOrEmpty(login.ReturnUrl) ? RedirectToAction("Index", "Home") : LocalRedirect(login.ReturnUrl);

            }
            else
            {
                ModelState.AddModelError("password", "UserName หรือ Password ไม่ถูกต้อง!");
                return View();
            }
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout(string name)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            //return Redirect("https://sso.kku.ac.th/logout.php?callback_logout=https://webappqshc.kku.ac.th/sso");
            return RedirectToAction("Index", "Home");
        }

        private async Task<List<string>> GetRolesList(IdentityUser user)
        {
            return new List<string>(await _userManager.GetRolesAsync(user));
        }
    }
}
