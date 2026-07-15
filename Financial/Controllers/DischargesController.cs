using ClosedXML.Excel;
using Financial.Models.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Financial.Controllers
{
    public class DischargesController : Controller
    {
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

        public List<VmDischargesPD> GetSPData (string PD,string SPName,string startdate, string enddate)
        {
            string connstr = "Server=10.67.67.64;user id=sa;password=Password@HO2021;Database=HealthObject;Trusted_Connection=False;TrustServerCertificate=True; " +
                    " Max Pool Size=400;Connect Timeout=600;";

            SqlConnection conn = null;
            SqlDataReader rdr = null;
            List<VmDischargesPD> _VmDischargesPD = new List<VmDischargesPD>();
            //VmLabResult _VmLabResult = new VmLabResult();
            conn = new SqlConnection(connstr);

            conn.Open();
            //EXEC pGetQSHCPatientDischarge @Date_From = '2023-02-06 00:00:00.000' , @Date_To = '2023-02-06 23:59:59.999'
            string sql = "EXEC "+ SPName + " @Date_From  = '" + startdate + "',  @Date_To  = '" + enddate + "'";
            SqlCommand cmdPatient = new SqlCommand(sql, conn);
            cmdPatient.CommandType = CommandType.Text;
            rdr = cmdPatient.ExecuteReader();
            
            if (rdr.HasRows)
            {
                while (rdr.Read())
                {
                    if (PD == "IPD")
                    {
                        _VmDischargesPD.Add(new VmDischargesPD
                        {
                            Id = Convert.ToInt32(rdr["ลำดับที่"]),
                            FullName = rdr["รายชื่อ"].ToString(),
                            TreatmentRight = rdr["สิทธิ์การรักษา"].ToString(),
                            //BillCon = rdr["เงื่อนไขการเรียกเก็บ"].ToString(),
                            HN = rdr["HN"].ToString(),
                            AN = rdr["AN"].ToString(),
                            Admit = rdr["Admit"].ToString(),
                            DCDate = rdr["D/C"].ToString(),
                            Amount = rdr["จำนวนเงิน"].ToString(),
                            //Clinic = rdr["คลินิก"].ToString(),

                        });
                    }
                    else
                    {
                        _VmDischargesPD.Add(new VmDischargesPD
                        {
                            Id = Convert.ToInt32(rdr["ลำดับที่"]),
                            FullName = rdr["รายชื่อ"].ToString(),
                            TreatmentRight = rdr["สิทธิ์การรักษา"].ToString(),
                            BillCon = rdr["เงื่อนไขการเรียกเก็บ"].ToString(),
                            HN = rdr["HN"].ToString(),
                            AN = rdr["VN"].ToString(),
                            DCDate = rdr["วันที่"].ToString(),
                            Amount = rdr["จำนวนเงิน"].ToString(),
                            Clinic = rdr["คลินิก"].ToString(),

                        });
                    }

                    
                }

                conn.Close();

                return _VmDischargesPD;
            }
            else
            {
                return null;
            }
        }


        // GET: DischargesController/IPD
        public ActionResult IPD(string start, string end)
        {
            var DNow = DateTime.Today.Date.AddDays(-1);
            var cStart = DNow.ToString("yyyy-MM-dd 00:00:00", new CultureInfo("en-GB"));
            var cEnd = DNow.ToString("yyyy-MM-dd 23:59:59", new CultureInfo("en-GB"));

            var startdate = string.IsNullOrEmpty(start) ? cStart : string.Format("{0} 00:00:00.000", start);
            var enddate = string.IsNullOrEmpty(end) ? cEnd : string.Format("{0} 23:59:59.999", end);

            ViewBag.fSdate = startdate;
            ViewBag.fEdate = enddate;
            var results = GetSPData("IPD","pGetQSHCPatientDischarge", startdate, enddate);

            return View(results);

        }
        [HttpPost]
        public IActionResult ExportExcelIP(string start, string end)
        {

            var results = GetSPData("IPD", "pGetQSHCPatientDischarge", start, end);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ER");
                var currentRow = 1;

                worksheet.Cell(currentRow, 1).Value = "ลำดับที่";
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 2).Value = "รายชื่อ";         
                worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 2).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 3).Value = "สิทธิ์การรักษา";    
                worksheet.Cell(currentRow, 3).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 3).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 4).Value = "HN";           
                worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 4).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 5).Value = "AN";           
                worksheet.Cell(currentRow, 5).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 5).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 6).Value = "Admit";        
                worksheet.Cell(currentRow, 6).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 6).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 7).Value = "D/C";          
                worksheet.Cell(currentRow, 7).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 7).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 8).Value = "จำนวนเงิน";
                worksheet.Cell(currentRow, 8).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 8).Style.Fill.SetBackgroundColor(XLColor.LightGray);




                foreach (var item in results)
                {
                    currentRow++;

                    worksheet.Cell(currentRow, 1).Value = item.Id;
                    worksheet.Cell(currentRow, 2).Value = item.FullName;
                    worksheet.Cell(currentRow, 3).Value = item.TreatmentRight;
                    worksheet.Cell(currentRow, 4).Value = item.HN;
                    worksheet.Cell(currentRow, 5).Value = item.AN;
                    worksheet.Cell(currentRow, 6).Value = item.Admit;
                    worksheet.Cell(currentRow, 7).Value = item.DCDate;
                    worksheet.Cell(currentRow, 8).Value = item.Amount;
                    worksheet.Column(1).AdjustToContents();
                    worksheet.Column(2).AdjustToContents();
                    worksheet.Column(3).AdjustToContents();
                    worksheet.Column(4).AdjustToContents();
                    worksheet.Column(5).AdjustToContents();
                    worksheet.Column(6).AdjustToContents();
                    worksheet.Column(7).AdjustToContents();
                    worksheet.Column(8).AdjustToContents();

                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();

                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        string.Format("{0}.csv", string.Format("D/C_IPD_{0}-{1}", start, end)));
                }
            }



        }

        // GET: DischargesController/IPD
        public ActionResult OPD(string start, string end)
        {
            var DNow = DateTime.Today.Date.AddDays(-1);
            var cStart = DNow.ToString("yyyy-MM-dd 00:00:00", new CultureInfo("en-GB"));
            var cEnd = DNow.ToString("yyyy-MM-dd 23:59:59", new CultureInfo("en-GB"));

            var startdate = string.IsNullOrEmpty(start) ? cStart : string.Format("{0} 00:00:00.000", start);
            var enddate = string.IsNullOrEmpty(end) ? cEnd : string.Format("{0} 23:59:59.999", end);

            ViewBag.fSdate = startdate;
            ViewBag.fEdate = enddate;
            var results = GetSPData("OPD", "pGetQSHCPatientDischargeOPD", startdate, enddate);

            return View(results);

        }
        [HttpPost]
        public IActionResult ExportExcelOP(string start, string end)
        {

            var results = GetSPData("OPD", "pGetQSHCPatientDischargeOPD", start, end);

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("ER");
                var currentRow = 1;

                worksheet.Cell(currentRow, 1).Value = "ลำดับที่";
                worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 1).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 2).Value = "รายชื่อ";
                worksheet.Cell(currentRow, 2).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 2).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 3).Value = "สิทธิ์การรักษา";
                worksheet.Cell(currentRow, 3).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 3).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 4).Value = "เงื่อนไขการเรียกเก็บ";
                worksheet.Cell(currentRow, 4).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 4).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 5).Value = "HN";
                worksheet.Cell(currentRow, 5).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 5).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 6).Value = "VN";
                worksheet.Cell(currentRow, 6).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 6).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 7).Value = "D/C";
                worksheet.Cell(currentRow, 7).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 7).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 8).Value = "จำนวนเงิน";
                worksheet.Cell(currentRow, 8).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 8).Style.Fill.SetBackgroundColor(XLColor.LightGray);
                worksheet.Cell(currentRow, 9).Value = "คลินิก";
                worksheet.Cell(currentRow, 9).Style.Font.Bold = true;
                worksheet.Cell(currentRow, 9).Style.Fill.SetBackgroundColor(XLColor.LightGray);




                foreach (var item in results)
                {
                    currentRow++;

                    worksheet.Cell(currentRow, 1).Value = item.Id;
                    worksheet.Cell(currentRow, 2).Value = item.FullName;
                    worksheet.Cell(currentRow, 3).Value = item.TreatmentRight;
                    worksheet.Cell(currentRow, 4).Value = item.BillCon;
                    worksheet.Cell(currentRow, 5).Value = item.HN;
                    worksheet.Cell(currentRow, 6).Value = item.AN;
                    worksheet.Cell(currentRow, 7).Value = item.DCDate;
                    worksheet.Cell(currentRow, 8).Value = item.Amount;
                    worksheet.Cell(currentRow, 9).Value = item.Clinic;
                    worksheet.Column(1).AdjustToContents();
                    worksheet.Column(2).AdjustToContents();
                    worksheet.Column(3).AdjustToContents();
                    worksheet.Column(4).AdjustToContents();
                    worksheet.Column(5).AdjustToContents();
                    worksheet.Column(6).AdjustToContents();
                    worksheet.Column(7).AdjustToContents();
                    worksheet.Column(8).AdjustToContents();
                    worksheet.Column(9).AdjustToContents();

                }

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();

                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        string.Format("{0}.xlsx", string.Format("D/C_OPD_{0}-{1}", start, end)));
                }
            }



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
        private static DataTable GetSPDataVal(string vConPro, string ProcedureName, DateTime DateFrom, DateTime DateTo)
        {
            SqlConnection vConnection = new SqlConnection(vConPro);
            SqlCommand vCommand = new SqlCommand(ProcedureName, vConnection);
            //CultureInfo VenG = new CultureInfo("en-GB");
            vCommand.CommandTimeout = 2000;
            vCommand.CommandType = CommandType.StoredProcedure;

            SqlParameter vParameterExpireDateFrom = new SqlParameter("@Date_From", SqlDbType.NVarChar);
            vParameterExpireDateFrom.Value = DateFrom ;
            vCommand.Parameters.Add(vParameterExpireDateFrom);

            SqlParameter vParameterExpireDateTo = new SqlParameter("@Date_To", SqlDbType.NVarChar);
            vParameterExpireDateTo.Value = DateTo ;
            vCommand.Parameters.Add(vParameterExpireDateTo);

            vConnection.Open();
            SqlDataReader ResulTreader = vCommand.ExecuteReader();
            DataTable result = new DataTable();
            result.Load(ResulTreader);
            vConnection.Close();
            return result;
        }
    }
}
