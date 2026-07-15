using Financial.Helpers;
using Financial.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Data;
using System.Drawing;
using System.Globalization;

namespace Financial.Controllers
{
    /// <summary>
    /// Controller สำหรับจัดการข้อมูล Discharges (IPD/OPD)
    /// </summary>
    [Authorize]
    public class DischargesController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<DischargesController> _logger;

        public DischargesController(
            IConfiguration configuration,
            ILogger<DischargesController> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        // GET: DischargesController
        public ActionResult Index()
        {
            return View();
        }

        // GET: DischargesController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        /// <summary>
        /// ดึงข้อมูลจาก Stored Procedure
        /// </summary>
        private List<VmDischargesPD>? GetSPData(string PD, string SPName, string startdate, string enddate)
        {
            // ใช้ connection string จาก configuration แทน hardcode
            var connstr = _configuration.GetConnectionString("HealthObject")
                ?? "Server=10.67.67.64;user id=sa;password=Password@HO2021;Database=HealthObject;Trusted_Connection=False;TrustServerCertificate=True;Max Pool Size=400;Connect Timeout=600;";

            try
            {
                using SqlConnection conn = new(connstr);
                conn.Open();

                // ใช้ parameterized query เพื่อป้องกัน SQL Injection
                string sql = $"EXEC {SPName} @Date_From = @StartDate, @Date_To = @EndDate";
                using SqlCommand cmdPatient = new(sql, conn);
                cmdPatient.CommandType = CommandType.Text;
                cmdPatient.Parameters.AddWithValue("@StartDate", startdate);
                cmdPatient.Parameters.AddWithValue("@EndDate", enddate);

                using SqlDataReader rdr = cmdPatient.ExecuteReader();
                List<VmDischargesPD> results = new();

                if (rdr.HasRows)
                {
                    while (rdr.Read())
                    {
                        if (PD == "IPD")
                        {
                            results.Add(new VmDischargesPD
                            {
                                Id = Convert.ToInt32(rdr["ลำดับที่"]),
                                FullName = rdr["รายชื่อ"]?.ToString() ?? "",
                                TreatmentRight = rdr["สิทธิ์การรักษา"]?.ToString() ?? "",
                                HN = rdr["HN"]?.ToString() ?? "",
                                AN = rdr["AN"]?.ToString() ?? "",
                                Admit = rdr["Admit"]?.ToString() ?? "",
                                DCDate = rdr["D/C"]?.ToString() ?? "",
                                Amount = rdr["จำนวนเงิน"]?.ToString() ?? "",
                            });
                        }
                        else
                        {
                            results.Add(new VmDischargesPD
                            {
                                Id = Convert.ToInt32(rdr["ลำดับที่"]),
                                FullName = rdr["รายชื่อ"]?.ToString() ?? "",
                                TreatmentRight = rdr["สิทธิ์การรักษา"]?.ToString() ?? "",
                                BillCon = rdr["เงื่อนไขการเรียกเก็บ"]?.ToString() ?? "",
                                HN = rdr["HN"]?.ToString() ?? "",
                                AN = rdr["VN"]?.ToString() ?? "",
                                DCDate = rdr["วันที่"]?.ToString() ?? "",
                                Amount = rdr["จำนวนเงิน"]?.ToString() ?? "",
                                Clinic = rdr["คลินิก"]?.ToString() ?? "",
                            });
                        }
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting discharge data for {PD} from {StartDate} to {EndDate}",
                    PD, startdate, enddate);
                return null;
            }
        }

        // GET: DischargesController/IPD
        public ActionResult IPD(string? start, string? end)
        {
            var DNow = DateTime.Now.Date.AddDays(-1);
            var cStart = DNow.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture);
            var cEnd = DNow.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture);

            var startdate = string.IsNullOrEmpty(start) ? cStart : $"{start} 00:00:00.000";
            var enddate = string.IsNullOrEmpty(end) ? cEnd : $"{end} 23:59:59.999";

            ViewBag.fSdate = startdate;
            ViewBag.fEdate = enddate;

            var results = GetSPData("IPD", "pGetQSHCPatientDischarge", startdate, enddate);

            return View(results);
        }

        /// <summary>
        /// Export ข้อมูล IPD เป็น Excel
        /// </summary>
        [HttpPost]
        public IActionResult ExportExcelIP(string start, string end)
        {
            var results = GetSPData("IPD", "pGetQSHCPatientDischarge", start, end);

            if (results == null || results.Count == 0)
            {
                return NotFound("ไม่พบข้อมูล");
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("IPD");

            // Header
            var headers = new[] { "ลำดับที่", "รายชื่อ", "สิทธิ์การรักษา", "HN", "AN", "Admit", "D/C", "จำนวนเงิน" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cells[1, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Data
            int row = 2;
            foreach (var item in results)
            {
                worksheet.Cells[row, 1].Value = item.Id;
                worksheet.Cells[row, 2].Value = item.FullName;
                worksheet.Cells[row, 3].Value = item.TreatmentRight;
                worksheet.Cells[row, 4].Value = item.HN;
                worksheet.Cells[row, 5].Value = item.AN;
                worksheet.Cells[row, 6].Value = item.Admit;
                worksheet.Cells[row, 7].Value = item.DCDate;
                worksheet.Cells[row, 8].Value = item.Amount;

                for (int col = 1; col <= 8; col++)
                {
                    worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var content = package.GetAsByteArray();
            var fileName = $"DC_IPD_{start}_{end}.xlsx";

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: DischargesController/OPD
        public ActionResult OPD(string? start, string? end)
        {
            var DNow = DateTime.Now.Date.AddDays(-1);
            var cStart = DNow.ToString("yyyy-MM-dd 00:00:00", CultureInfo.InvariantCulture);
            var cEnd = DNow.ToString("yyyy-MM-dd 23:59:59", CultureInfo.InvariantCulture);

            var startdate = string.IsNullOrEmpty(start) ? cStart : $"{start} 00:00:00.000";
            var enddate = string.IsNullOrEmpty(end) ? cEnd : $"{end} 23:59:59.999";

            ViewBag.fSdate = startdate;
            ViewBag.fEdate = enddate;

            var results = GetSPData("OPD", "pGetQSHCPatientDischargeOPD", startdate, enddate);

            return View(results);
        }

        /// <summary>
        /// Export ข้อมูล OPD เป็น Excel
        /// </summary>
        [HttpPost]
        public IActionResult ExportExcelOP(string start, string end)
        {
            var results = GetSPData("OPD", "pGetQSHCPatientDischargeOPD", start, end);

            if (results == null || results.Count == 0)
            {
                return NotFound("ไม่พบข้อมูล");
            }

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            using var package = new ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("OPD");

            // Header
            var headers = new[] { "ลำดับที่", "รายชื่อ", "สิทธิ์การรักษา", "เงื่อนไขการเรียกเก็บ", "HN", "VN", "D/C", "จำนวนเงิน", "คลินิก" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = worksheet.Cells[1, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                cell.Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            // Data
            int row = 2;
            foreach (var item in results)
            {
                worksheet.Cells[row, 1].Value = item.Id;
                worksheet.Cells[row, 2].Value = item.FullName;
                worksheet.Cells[row, 3].Value = item.TreatmentRight;
                worksheet.Cells[row, 4].Value = item.BillCon;
                worksheet.Cells[row, 5].Value = item.HN;
                worksheet.Cells[row, 6].Value = item.AN;
                worksheet.Cells[row, 7].Value = item.DCDate;
                worksheet.Cells[row, 8].Value = item.Amount;
                worksheet.Cells[row, 9].Value = item.Clinic;

                for (int col = 1; col <= 9; col++)
                {
                    worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                row++;
            }

            // Auto-fit columns
            worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();

            var content = package.GetAsByteArray();
            var fileName = $"DC_OPD_{start}_{end}.xlsx";

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // GET: DischargesController/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: DischargesController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: DischargesController/Edit/5
        public ActionResult Edit(int id)
        {
            return View();
        }

        // POST: DischargesController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        // GET: DischargesController/Delete/5
        public ActionResult Delete(int id)
        {
            return View();
        }

        // POST: DischargesController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Delete(int id, IFormCollection collection)
        {
            try
            {
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                return View();
            }
        }

        /// <summary>
        /// ดึงข้อมูลจาก Stored Procedure แบบ DataTable (ใช้สำหรับ legacy code)
        /// </summary>
        private static DataTable GetSPDataVal(string vConPro, string ProcedureName, DateTime DateFrom, DateTime DateTo)
        {
            using SqlConnection vConnection = new(vConPro);
            using SqlCommand vCommand = new(ProcedureName, vConnection)
            {
                CommandTimeout = 2000,
                CommandType = CommandType.StoredProcedure
            };

            vCommand.Parameters.Add(new SqlParameter("@Date_From", SqlDbType.NVarChar) { Value = DateFrom });
            vCommand.Parameters.Add(new SqlParameter("@Date_To", SqlDbType.NVarChar) { Value = DateTo });

            vConnection.Open();
            using SqlDataReader reader = vCommand.ExecuteReader();
            DataTable result = new();
            result.Load(reader);

            return result;
        }
    }
}
