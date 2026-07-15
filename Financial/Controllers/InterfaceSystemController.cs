using Financial.Database.InterfaceSystem.Contexts;
using Financial.Database.InterfaceSystem.Models;
using Financial.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;

namespace Financial.Controllers
{
    /// <summary>
    /// Controller สำหรับจัดการระบบ Interface (EDC, CSMBS, etc.)
    /// </summary>
    [Authorize]
    public class InterfaceSystemController : Controller
    {
        private string? _connectionString;
        private readonly IConfiguration _configuration;

        private InterfaceSystemDbContext _InterfaceSystemDbContext;

        public InterfaceSystemController(IConfiguration configuration, InterfaceSystemDbContext InterfaceSystemDbContext)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("InterfaceSystem");
            _InterfaceSystemDbContext = InterfaceSystemDbContext;
        }
        // GET: InterfaceEDC
        public IActionResult Index()
        {
            return View();
        }
        [HttpPost]
        public IActionResult CheckFileExists(string destinationPath, string fileName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(destinationPath) || string.IsNullOrWhiteSpace(fileName))
                {
                    return Json(new { exists = false, message = "Invalid parameters" });
                }

                var fullPath = Path.Combine(destinationPath, fileName);

                if (System.IO.File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    return Json(new
                    {
                        exists = true,
                        fileName = fileName,
                        filePath = fullPath,
                        fileSize = fileInfo.Length,
                        lastModified = fileInfo.LastWriteTime,
                        createdDate = fileInfo.CreationTime
                    });
                }

                return Json(new { exists = false });
            }
            catch (Exception ex)
            {
                return Json(new { exists = false, error = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult ImportEDC(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            if (string.IsNullOrEmpty(extension) || extension.ToLower() != ".txt")
            {
                return Json(new { exists = false, error = "ไฟล์ต้องเป็น .txt เท่านั้น" });
            }

            if (!file.FileName.StartsWith("HCG14584"))
            {
                return Json(new { exists = false, error = "ชื่อไฟล์ต้องขึ้นต้นด้วย HCG14584" });
            }

            string vInfCode = "EDC";
            string vInfName = "Electronic Data Capture";
            var userId = Convert.ToInt32(User.Claims.FirstOrDefault(c => c.Type == "LoginUID")?.Value);
            var MUser = userId;
            var CUser = userId;
            int vRecord = 1;
            var _list = new List<InterfaceItem>();

            try
            {
                var lines = new List<string>();
                using (var streamReader = new StreamReader(file.OpenReadStream()))
                {
                    string line;
                    while ((line = streamReader.ReadLine()) != null)
                    {
                        lines.Add(line);
                    }
                }

                foreach (var item in lines)
                {
                    var interfaceItem = new InterfaceItem
                    {
                        FileName = file.FileName,
                        Record = vRecord++,
                        Data = item,
                        InterfaceType = vInfCode,
                        Mwhen = DateTime.Now,
                        Muser = MUser,
                        Cwhen = DateTime.Now,
                        Cuser = CUser,
                        StatusFlag = "A",
                        ProcessStatus = "Initial"
                    };
                    _list.Add(interfaceItem);
                }

                _InterfaceSystemDbContext.InterfaceItems.AddRange(_list);
                _InterfaceSystemDbContext.SaveChanges();

                // Fix for CS0103: Define modeParameter explicitly
                var modeParameter = new SqlParameter("@Mode", SqlDbType.VarChar) { Value = "YourModeValue" };

                // Fix for CS1061: Use ExecuteSqlRaw instead of ExecuteSqlCommand
                _InterfaceSystemDbContext.Database.ExecuteSqlRaw("EXEC pGetQSHCInterfaceEDCResult @Mode", modeParameter);

                return Json(new { exists = true });
            }
            catch (Exception ex)
            {
                return Json(new { exists = false, error = ex.Message });
            }
        }
        [HttpPost]
        public async Task<IActionResult> UploadFile(IFormFile file, string destinationPath, bool overwrite = false)
        {
            var result = new UploadResult();

            try
            {
                // ตรวจสอบว่ามีไฟล์หรือไม่
                if (file == null || file.Length == 0)
                {
                    result.Success = false;
                    result.Message = "กรุณาเลือกไฟล์ที่ต้องการอัปโหลด";
                    return Json(result);
                }

                // ตรวจสอบ destination path
                if (string.IsNullOrWhiteSpace(destinationPath))
                {
                    result.Success = false;
                    result.Message = "กรุณาระบุ destination path";
                    return Json(result);
                }

                // ตรวจสอบขนาดไฟล์ (จำกัดที่ 50MB)
                if (file.Length > 50 * 1024 * 1024)
                {
                    result.Success = false;
                    result.Message = "ขนาดไฟล์เกิน 50MB";
                    return Json(result);
                }

                // สร้าง full path
                var fileName = Path.GetFileName(file.FileName);
                var fullPath = Path.Combine(destinationPath, fileName);

                // ตรวจสอบว่า directory มีอยู่หรือไม่
                if (!Directory.Exists(destinationPath))
                {
                    result.Success = false;
                    result.Message = $"ไม่พบ directory: {destinationPath}";
                    return Json(result);
                }

                // ตรวจสอบว่าไฟล์มีอยู่แล้วหรือไม่
                if (System.IO.File.Exists(fullPath) && !overwrite)
                {
                    var existingFileInfo = new FileInfo(fullPath);
                    result.Success = false;
                    result.FileExists = true;
                    result.Message = $"มีไฟล์ '{fileName}' อยู่แล้ว";
                    result.ExistingFileInfo = new ExistingFileDetails
                    {
                        FileName = fileName,
                        FilePath = fullPath,
                        FileSize = existingFileInfo.Length,
                        LastModified = existingFileInfo.LastWriteTime,
                        CreatedDate = existingFileInfo.CreationTime
                    };
                    return Json(result);
                }

                // อัปโหลดไฟล์
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // ตรวจสอบว่าไฟล์ถูกสร้างสำเร็จ
                if (System.IO.File.Exists(fullPath))
                {
                    var fileInfo = new FileInfo(fullPath);
                    result.Success = true;
                    result.Message = "อัปโหลดไฟล์สำเร็จ";
                    result.FileName = fileName;
                    result.FilePath = fullPath;
                    result.FileSize = fileInfo.Length;
                    result.UploadTime = DateTime.Now;

                    //_logger.LogInformation($"File uploaded successfully: {fullPath}");
                }
                else
                {
                    result.Success = false;
                    result.Message = "เกิดข้อผิดพลาดในการสร้างไฟล์";
                }
            }
            catch (UnauthorizedAccessException)
            {
                result.Success = false;
                result.Message = "ไม่มีสิทธิ์เข้าถึง directory ปลายทาง";
                //_logger.LogError($"Unauthorized access to: {destinationPath}");
            }
            catch (DirectoryNotFoundException)
            {
                result.Success = false;
                result.Message = "ไม่พบ directory ปลายทาง";
                //_logger.LogError($"Directory not found: {destinationPath}");
            }
            catch (IOException ex)
            {
                result.Success = false;
                result.Message = $"เกิดข้อผิดพลาดในการเขียนไฟล์: {ex.Message}";
                //_logger.LogError(ex, "IO Exception during file upload");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"เกิดข้อผิดพลาดไม่คาดคิด: {ex.Message}";
                //_logger.LogError(ex, "Unexpected error during file upload");
            }

            return Json(result);
        }

        public class PathRequest
        {
            public string Path { get; set; }
        }
        [HttpPost]
        public IActionResult TestConnection([FromBody] PathRequest request)
        {
            try
            {
                if (Directory.Exists(request.Path))
                {
                    return Json(new { success = true, message = "เชื่อมต่อสำเร็จ" });
                }
                else
                {
                    return Json(new { success = false, message = $"ไม่พบ directory:{request.Path}" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"เกิดข้อผิดพลาด: {ex.Message}" });
            }
        }
        public async Task<IActionResult> EDCIndex()
        {
            var patientEDCRegister = await _InterfaceSystemDbContext.PatientEdcregisters.OrderByDescending(o=>o.Uid).Take(50).ToListAsync();
            return View(patientEDCRegister);
        }



        [HttpPost]
        public async Task<IActionResult> UploadEDCFile(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded.");
            }


            var path = await (from i in _InterfaceSystemDbContext.Interfaces
                              join ic in _InterfaceSystemDbContext.InterfaceConfigs on i.Uid equals ic.InterfaceUid
                              where i.Code == "EDC" && !string.IsNullOrEmpty(ic.SourceDirectory)
                              select ic.SourceDirectory)
                  .FirstOrDefaultAsync();


            List<string[]> data = new List<string[]>();

            using (var stream = new StreamReader(file.OpenReadStream()))
            {
                while (!stream.EndOfStream)
                {
                    var line = await stream.ReadLineAsync();
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var values = line.Split('|'); // แยกค่าด้วย "|"
                        data.Add(values);
                    }
                }
            }

            return Ok(new { message = "อัปโหลดสำเร็จ", data });
            //var filePath = Path.Combine("Uploads", file.FileName);

            //using (var stream = new FileStream(filePath, FileMode.Create))
            //{
            //    await file.CopyToAsync(stream);
            //}

            //string result = await ImportEDCAsync(_connectionString, filePath);
            //return Ok(new { Message = result });
        }


        private async Task<string> ImportEDCAsync(string connectionString, string filePath)
        {
            string interfaceCode = "EDC";
            string interfaceName = "Electronic Data Capture";

            try
            {
                string query = "INSERT INTO InterfaceItem (Filename, Record, Data, InterfaceType, MWhen, MUser, CWhen, CUser, StatusFlag, ProcessStatus) VALUES (@filename, @record, @data, @interfaceType, GETDATE(), '1', GETDATE(), '1', 'A', 'Initial')";

                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    int recordCount = 1;
                    string[] lines = await System.IO.File.ReadAllLinesAsync(filePath);

                    foreach (string line in lines)
                    {
                        using (SqlCommand command = new SqlCommand(query, connection))
                        {
                            command.Parameters.AddWithValue("@filename", Path.GetFileName(filePath));
                            command.Parameters.AddWithValue("@record", recordCount);
                            command.Parameters.AddWithValue("@data", line);
                            command.Parameters.AddWithValue("@interfaceType", interfaceCode);

                            await command.ExecuteNonQueryAsync();
                        }
                        recordCount++;
                    }
                }

                return interfaceName + " import complete.";
            }
            catch (Exception ex)
            {
                //_logger.LogError(ex, "Error importing file.");
                return interfaceName + " error: " + ex.Message;
            }
        }
    }
}
