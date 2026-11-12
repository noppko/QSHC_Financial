using Financial.Models;
using Financial.Services;
using Financial.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Financial.Controllers
{
    /// <summary>
    /// จัดการค่าอ้างอิง (ReferenceValues ในฐาน QSHC_Financial) แยกตามหมวด — Admin เท่านั้น
    /// - เพิ่ม/แก้ไข/ลบ (soft delete) ค่าทั่วไป · ค่าระดับระบบ (IsSystem) แก้ได้เฉพาะชื่อ/รายละเอียด ลบไม่ได้
    /// - ลากเรียงลำดับ (DisplayOrder) แล้วกดบันทึก
    /// </summary>
    [Authorize(Policy = AppPolicies.ADMIN)]
    public class ReferenceValuesController : Controller
    {
        private readonly IReferenceValueService _service;
        private readonly ILogger<ReferenceValuesController> _logger;

        public ReferenceValuesController(IReferenceValueService service, ILogger<ReferenceValuesController> logger)
        {
            _service = service;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index(string? category)
        {
            var model = new ReferenceValuesViewModel();
            try
            {
                model.Categories = await _service.GetCategoriesAsync();
                model.Category = Normalize(category) ?? model.Categories.FirstOrDefault()?.Category;
                if (model.Category is not null)
                    model.Values = (await LoadCategoryAsync(model.Category)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cannot load reference values");
                model.Error = "อ่านข้อมูลไม่ได้ — ตรวจการเชื่อมต่อฐาน QSHC_Financial (DefaultConnection)";
            }
            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Create(string? category)
        {
            ViewData["Categories"] = await SafeCategoriesAsync();
            return View("Edit", new ReferenceValue { Category = Normalize(category) ?? "" });
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var value = await _service.GetByIdAsync(id);
            if (value is null) return NotFound();
            ViewData["Categories"] = await SafeCategoriesAsync();
            return View(value);
        }

        [HttpPost]
        public async Task<IActionResult> Save([Bind("Id,Category,Code,NameTH,NameEN,Description,DisplayOrder")] ReferenceValue value)
        {
            // ตรวจรูปแบบที่ service (ข้อความภาษาไทยเดียวกันทุกช่องทาง) — ช่อง navigation ไม่ได้ส่งมาจากฟอร์ม
            ModelState.Clear();
            var result = await _service.TrySaveAsync(value);
            if (!result.Ok)
            {
                ModelState.AddModelError(string.Empty, result.Error ?? "บันทึกไม่สำเร็จ");
                if (value.Id > 0 && await _service.GetByIdAsync(value.Id) is { IsSystem: true }) value.IsSystem = true;
                ViewData["Categories"] = await SafeCategoriesAsync();
                return View("Edit", value);
            }

            _logger.LogInformation("Reference value {Category}/{Code} saved by {User}", value.Category, value.Code, User.Identity?.Name);
            TempData["Message"] = $"บันทึก {value.Category} / {value.Code} แล้ว";
            return RedirectToAction(nameof(Index), new { category = value.Category });
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var value = await _service.GetByIdAsync(id);
            if (value is null) return NotFound();

            if (await _service.DeleteAsync(id))
            {
                _logger.LogInformation("Reference value {Category}/{Code} deleted by {User}", value.Category, value.Code, User.Identity?.Name);
                TempData["Message"] = $"ลบ {value.Category} / {value.Code} แล้ว";
            }
            else
            {
                TempData["Error"] = value.IsSystem ? "ค่าระดับระบบลบไม่ได้" : "ลบไม่สำเร็จ";
            }
            return RedirectToAction(nameof(Index), new { category = value.Category });
        }

        /// <summary>บันทึกลำดับหลังลากเรียง (AJAX, JSON) — ส่ง id ของทุกรายการในหมวดตามลำดับใหม่</summary>
        [HttpPost]
        public async Task<IActionResult> Reorder([FromBody] ReorderRequest request)
        {
            var category = Normalize(request?.Category);
            if (category is null || request!.Ids is not { Count: > 0 })
                return BadRequest(new { message = "ข้อมูลไม่ครบ" });

            if (!await _service.ReorderAsync(category, request.Ids))
                return BadRequest(new { message = "บันทึกลำดับไม่สำเร็จ — รายการในหมวดอาจมีการเปลี่ยนแปลง กรุณารีเฟรชหน้า" });

            _logger.LogInformation("Reference values {Category} reordered by {User}", category, User.Identity?.Name);
            var values = await LoadCategoryAsync(category);
            return Json(new { message = "บันทึกลำดับแล้ว", items = values.Select(v => new { v.Id, v.DisplayOrder }) });
        }

        public sealed record ReorderRequest(string? Category, List<int>? Ids);

        private async Task<IEnumerable<ReferenceValue>> LoadCategoryAsync(string category)
        {
            // อ่านตรงจากฐาน (ไม่ใช้ cache) เพื่อให้เห็นค่าล่าสุดเสมอ
            _service.InvalidateCache(category);
            return await _service.GetByCategoryAsync(category);
        }

        private async Task<List<ReferenceCategorySummary>> SafeCategoriesAsync()
        {
            try { return await _service.GetCategoriesAsync(); }
            catch (Exception ex) { _logger.LogWarning(ex, "Cannot load reference categories"); return []; }
        }

        private static string? Normalize(string? category)
            => string.IsNullOrWhiteSpace(category) ? null : category.Trim().ToUpperInvariant();
    }

    public sealed class ReferenceValuesViewModel
    {
        public List<ReferenceCategorySummary> Categories { get; set; } = [];
        public string? Category { get; set; }
        public List<ReferenceValue> Values { get; set; } = [];
        public string? Error { get; set; }
    }
}
