using Financial.Helpers;
using Financial.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Financial.Models;
using Financial.Models.ViewModels;
using Financial.Services.Discharges;
using Microsoft.AspNetCore.Mvc;

namespace Financial.Controllers
{
    /// <summary>
    /// Controller สำหรับจัดการข้อมูล Discharges (IPD/OPD)
    /// </summary>
    [Authorize(Policy = AppPolicies.FINANCE)]
    public class DischargesController : Controller
    {
        private const string EXCEL_CONTENT_TYPE = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        private readonly IDischargeService _dischargeService;
        private readonly TimeProvider _timeProvider;

        public DischargesController(IDischargeService dischargeService, TimeProvider timeProvider)
        {
            _dischargeService = dischargeService;
            _timeProvider = timeProvider;
        }

        // GET: /Discharges
        public IActionResult Index() => View();

        // GET: /Discharges/IPD?start=yyyy-MM-dd&end=yyyy-MM-dd&right=สิทธิ์
        public Task<IActionResult> IPD(string? start, string? end, string? right, CancellationToken cancellationToken)
            => List(PatientTypeCodes.IPD, start, end, right, cancellationToken);

        // GET: /Discharges/OPD?start=yyyy-MM-dd&end=yyyy-MM-dd&right=สิทธิ์
        public Task<IActionResult> OPD(string? start, string? end, string? right, CancellationToken cancellationToken)
            => List(PatientTypeCodes.OPD, start, end, right, cancellationToken);

        /// <summary>
        /// Export ข้อมูลเป็น Excel ตามตัวกรองเดียวกับหน้าจอ (ช่วงวันที่ + สิทธิ์) — ใช้ผลลัพธ์จาก cache ถ้าเพิ่งค้นหา
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Export(string? type, string? start, string? end, string? right, CancellationToken cancellationToken)
        {
            type = PatientTypeCodes.Normalize(type);
            if (!DateRange.TryParse(start, end, Today(), out var range, out var error))
            {
                TempData["Error"] = error;
                return RedirectToAction(PatientTypeCodes.Abbreviation(type), new { start, end, right });
            }

            var result = (await _dischargeService.GetAsync(type, range, cancellationToken)).FilterByRight(right);
            if (!result.Succeeded || result.Items.Count == 0)
            {
                TempData["Error"] = result.ErrorMessage ?? "ไม่พบข้อมูลสำหรับส่งออก";
                return RedirectToAction(PatientTypeCodes.Abbreviation(type), new { start = range.StartInput, end = range.EndInput, right });
            }

            var content = type == PatientTypeCodes.IPD
                ? ExcelHelper.CreateExcel(result.Items.ToList(), "IPD", new Dictionary<string, Func<VmDischargesPD, object>>
                {
                    ["ลำดับที่"] = x => x.Id,
                    ["รายชื่อ"] = x => x.FullName,
                    ["สิทธิ์การรักษา"] = x => x.TreatmentRight,
                    ["เงื่อนไขการเรียกเก็บ"] = x => x.BillCon,
                    ["HN"] = x => x.HN,
                    ["AN"] = x => x.AN,
                    ["Admit"] = x => x.Admit,
                    ["D/C"] = x => x.DCDate,
                    ["จำนวนเงิน"] = x => DischargeQueryResult.ParseAmount(x.Amount),
                    ["หอผู้ป่วย"] = x => x.Clinic,
                })
                : ExcelHelper.CreateExcel(result.Items.ToList(), "OPD", new Dictionary<string, Func<VmDischargesPD, object>>
                {
                    ["ลำดับที่"] = x => x.Id,
                    ["รายชื่อ"] = x => x.FullName,
                    ["สิทธิ์การรักษา"] = x => x.TreatmentRight,
                    ["เงื่อนไขการเรียกเก็บ"] = x => x.BillCon,
                    ["HN"] = x => x.HN,
                    ["VN"] = x => x.AN,
                    ["วันที่"] = x => x.DCDate,
                    ["จำนวนเงิน"] = x => DischargeQueryResult.ParseAmount(x.Amount),
                    ["คลินิก"] = x => x.Clinic,
                });

            return File(content, EXCEL_CONTENT_TYPE, ExportFileName(type, range, result.RightFilter));
        }

        private async Task<IActionResult> List(string type, string? start, string? end, string? right, CancellationToken cancellationToken)
        {
            DischargeQueryResult result;
            if (DateRange.TryParse(start, end, Today(), out var range, out var error))
            {
                result = await _dischargeService.GetAsync(type, range, cancellationToken);
            }
            else
            {
                result = new DischargeQueryResult(type, range, [], error);
            }

            return View("List", result.FilterByRight(right));
        }

        /// <summary>เช่น DC_OPD_2026-10-01_2026-10-31_บัตรทอง.xlsx (ตัดอักขระที่ใช้ในชื่อไฟล์ไม่ได้)</summary>
        internal static string ExportFileName(string type, DateRange range, string? right)
        {
            var name = $"DC_{PatientTypeCodes.Abbreviation(type)}_{range.StartInput}_{range.EndInput}";
            if (!string.IsNullOrWhiteSpace(right))
            {
                var safe = new string(right.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
                name += "_" + (safe.Length > 50 ? safe[..50] : safe);
            }
            return name + ".xlsx";
        }

        private DateOnly Today() => DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);
    }
}
