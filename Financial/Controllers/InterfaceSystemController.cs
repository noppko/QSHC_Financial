using Financial.Database.InterfaceSystem.Contexts;
using Financial.Services.Authorization;
using Microsoft.AspNetCore.Authorization;
using Financial.Database.InterfaceSystem.Models;
using Financial.Models.ViewModels;
using Financial.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Data;

namespace Financial.Controllers
{
    /// <summary>
    /// Controller สำหรับจัดการระบบ Interface (EDC, CSMBS, etc.)
    /// </summary>
    [Authorize(Policy = AppPolicies.FINANCE)]
    public class InterfaceSystemController : Controller
    {
        private const string EDC_FILE_PREFIX = "HCG14584";

        private readonly InterfaceSystemDbContext _db;
        private readonly FileUploadOptions _uploadOptions;
        private readonly ILogger<InterfaceSystemController> _logger;

        public InterfaceSystemController(
            InterfaceSystemDbContext db,
            IOptions<FileUploadOptions> uploadOptions,
            ILogger<InterfaceSystemController> logger)
        {
            _db = db;
            _uploadOptions = uploadOptions.Value;
            _logger = logger;
        }

        // GET: /InterfaceSystem
        public IActionResult Index()
        {
            ViewData["DestinationPath"] = _uploadOptions.DefaultDestinationPath;
            ViewData["MaxFileSizeMb"] = _uploadOptions.MaxFileSizeInBytes / 1024 / 1024;
            return View();
        }

        /// <summary>
        /// ทดสอบการเชื่อมต่อโฟลเดอร์ปลายทางที่ตั้งค่าไว้ใน server
        /// </summary>
        [HttpPost]
        public IActionResult TestConnection()
        {
            var path = _uploadOptions.DefaultDestinationPath;
            try
            {
                return Directory.Exists(path)
                    ? Json(new { success = true, message = "เชื่อมต่อโฟลเดอร์ปลายทางสำเร็จ" })
                    : Json(new { success = false, message = "ไม่พบโฟลเดอร์ปลายทาง หรือไม่มีสิทธิ์เข้าถึง" });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _logger.LogWarning(ex, "Test connection failed for {Path}", path);
                return Json(new { success = false, message = "ไม่มีสิทธิ์เข้าถึงโฟลเดอร์ปลายทาง" });
            }
        }

        [HttpPost]
        public IActionResult CheckFileExists(string fileName)
        {
            if (!UploadPathGuard.TryResolve(_uploadOptions.DefaultDestinationPath, fileName, out var fullPath))
            {
                return Json(new { exists = false, message = "ชื่อไฟล์ไม่ถูกต้อง" });
            }

            if (!System.IO.File.Exists(fullPath))
            {
                return Json(new { exists = false });
            }

            var fileInfo = new FileInfo(fullPath);
            return Json(new
            {
                exists = true,
                fileName = fileInfo.Name,
                fileSize = fileInfo.Length,
                lastModified = fileInfo.LastWriteTime,
                createdDate = fileInfo.CreationTime
            });
        }

        /// <summary>
        /// นำเข้าไฟล์ EDC (.txt) ลงตาราง InterfaceItem แล้วเรียก SP ประมวลผล
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<IActionResult> ImportEDC(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return Json(new { success = false, error = "กรุณาเลือกไฟล์" });
            }

            if (file.Length > _uploadOptions.MaxFileSizeInBytes)
            {
                return Json(new { success = false, error = $"ขนาดไฟล์เกิน {_uploadOptions.MaxFileSizeInBytes / 1024 / 1024}MB" });
            }

            var fileName = Path.GetFileName(file.FileName);
            if (!string.Equals(Path.GetExtension(fileName), ".txt", StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, error = "ไฟล์ต้องเป็น .txt เท่านั้น" });
            }

            if (!fileName.StartsWith(EDC_FILE_PREFIX, StringComparison.OrdinalIgnoreCase))
            {
                return Json(new { success = false, error = $"ชื่อไฟล์ต้องขึ้นต้นด้วย {EDC_FILE_PREFIX}" });
            }

            // หมายเหตุ: Auth API ยังไม่ส่ง LoginUID มา จึงได้ 0 — ต้องเพิ่ม claim เมื่อ API รองรับ
            _ = int.TryParse(User.FindFirst("LoginUID")?.Value, out var userId);
            var now = DateTime.Now;
            var items = new List<InterfaceItem>();

            try
            {
                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    var record = 1;
                    string? line;
                    while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                    {
                        items.Add(new InterfaceItem
                        {
                            FileName = fileName,
                            Record = record++,
                            Data = line,
                            InterfaceType = "EDC",
                            Mwhen = now,
                            Muser = userId,
                            Cwhen = now,
                            Cuser = userId,
                            StatusFlag = "A",
                            ProcessStatus = "Initial"
                        });
                    }
                }

                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                _db.InterfaceItems.AddRange(items);
                await _db.SaveChangesAsync(cancellationToken);

                // TODO: ยืนยันค่า @Mode ที่ถูกต้องกับเจ้าของ SP (ค่าเดิมในโค้ดเป็นค่าตัวอย่าง)
                var modeParameter = new SqlParameter("@Mode", SqlDbType.VarChar) { Value = "YourModeValue" };
                await _db.Database.ExecuteSqlRawAsync("EXEC pGetQSHCInterfaceEDCResult @Mode", [modeParameter], cancellationToken);

                await transaction.CommitAsync(cancellationToken);

                _logger.LogInformation("Imported EDC file {FileName} ({Count} records) by {User}", fileName, items.Count, User.Identity?.Name);
                return Json(new { success = true, records = items.Count });
            }
            catch (Exception ex) when (ex is DbUpdateException or SqlException or IOException or InvalidOperationException)
            {
                _logger.LogError(ex, "Import EDC failed for {FileName}", fileName);
                return Json(new { success = false, error = "นำเข้าข้อมูลไม่สำเร็จ กรุณาตรวจสอบไฟล์และลองใหม่" });
            }
        }

        /// <summary>
        /// อัปโหลดไฟล์ไปยังโฟลเดอร์ปลายทางที่ตั้งค่าไว้ (FileUpload:DefaultDestinationPath)
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(100 * 1024 * 1024)]
        public async Task<IActionResult> UploadFile(IFormFile? file, bool overwrite = false, CancellationToken cancellationToken = default)
        {
            var result = new UploadResult();

            if (file is null || file.Length == 0)
            {
                result.Message = "กรุณาเลือกไฟล์ที่ต้องการอัปโหลด";
                return Json(result);
            }

            if (file.Length > _uploadOptions.MaxFileSizeInBytes)
            {
                result.Message = $"ขนาดไฟล์เกิน {_uploadOptions.MaxFileSizeInBytes / 1024 / 1024}MB";
                return Json(result);
            }

            var fileName = Path.GetFileName(file.FileName);
            if (!_uploadOptions.IsAllowedExtension(fileName))
            {
                result.Message = $"อนุญาตเฉพาะไฟล์ {string.Join(", ", _uploadOptions.AllowedExtensions)}";
                return Json(result);
            }

            var destination = _uploadOptions.DefaultDestinationPath;
            if (!UploadPathGuard.TryResolve(destination, fileName, out var fullPath))
            {
                result.Message = "ชื่อไฟล์ไม่ถูกต้อง";
                return Json(result);
            }

            try
            {
                if (!Directory.Exists(destination))
                {
                    result.Message = "ไม่พบโฟลเดอร์ปลายทาง";
                    return Json(result);
                }

                if (System.IO.File.Exists(fullPath) && !overwrite)
                {
                    var existing = new FileInfo(fullPath);
                    result.FileExists = true;
                    result.Message = $"มีไฟล์ '{fileName}' อยู่แล้ว";
                    result.ExistingFileInfo = new ExistingFileDetails
                    {
                        FileName = fileName,
                        FilePath = fullPath,
                        FileSize = existing.Length,
                        LastModified = existing.LastWriteTime,
                        CreatedDate = existing.CreationTime
                    };
                    return Json(result);
                }

                await using (var stream = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    await file.CopyToAsync(stream, cancellationToken);
                }

                var info = new FileInfo(fullPath);
                result.Success = true;
                result.Message = "อัปโหลดไฟล์สำเร็จ";
                result.FileName = fileName;
                result.FilePath = fullPath;
                result.FileSize = info.Length;
                result.UploadTime = DateTime.Now;

                _logger.LogInformation("File uploaded: {Path} by {User}", fullPath, User.Identity?.Name);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Unauthorized access to: {Destination}", destination);
                result.Message = "ไม่มีสิทธิ์เข้าถึงโฟลเดอร์ปลายทาง";
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error during file upload to {Destination}", destination);
                result.Message = "เกิดข้อผิดพลาดในการเขียนไฟล์";
            }

            return Json(result);
        }

        // GET: /InterfaceSystem/EDCIndex
        public async Task<IActionResult> EDCIndex(CancellationToken cancellationToken)
        {
            var records = await _db.PatientEdcregisters
                .AsNoTracking()
                .OrderByDescending(o => o.Uid)
                .Take(50)
                .ToListAsync(cancellationToken);
            return View(records);
        }

        /// <summary>
        /// อ่านไฟล์ EDC (คั่นด้วย |) เพื่อแสดงตัวอย่างก่อนนำเข้า — ไม่บันทึกลงฐานข้อมูล
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> PreviewEDCFile(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return BadRequest(new { message = "กรุณาเลือกไฟล์" });
            }

            const int MAX_ROWS = 500;
            var data = new List<string[]>();
            var total = 0;

            using var stream = new StreamReader(file.OpenReadStream());
            string? line;
            while ((line = await stream.ReadLineAsync(cancellationToken)) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                total++;
                if (data.Count < MAX_ROWS) data.Add(line.Split('|'));
            }

            return Ok(new { message = "อ่านไฟล์สำเร็จ", total, data });
        }
    }
}
