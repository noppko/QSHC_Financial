using Financial.Models;
using Financial.Models.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Globalization;

namespace Financial.Services.Discharges
{
    /// <summary>
    /// ผลการค้นหา Discharge — แยก "ไม่มีข้อมูล" ออกจาก "เกิดข้อผิดพลาด"
    /// </summary>
    public sealed record DischargeQueryResult(string Type, DateRange Range, IReadOnlyList<VmDischargesPD> Items, string? ErrorMessage = null)
    {
        /// <summary>ค่าที่ใช้แทนสิทธิ์ว่าง ในตัวกรองและรายการสิทธิ์</summary>
        public const string NO_RIGHT = "(ไม่ระบุสิทธิ์)";

        public bool Succeeded => ErrorMessage is null;

        public decimal TotalAmount => Items.Sum(i => ParseAmount(i.Amount));

        /// <summary>สิทธิ์ที่กำลังกรอง (null = ทั้งหมด)</summary>
        public string? RightFilter { get; init; }

        /// <summary>สิทธิ์ทั้งหมดที่พบในช่วงวันที่ (ก่อนกรอง) พร้อมจำนวนและยอดเงิน — เรียงตามยอดเงิน</summary>
        public IReadOnlyList<RightSummary> Rights { get; init; } = [];

        /// <summary>จำนวนรายการก่อนกรองสิทธิ์</summary>
        public int UnfilteredCount { get; init; }

        public static decimal ParseAmount(string? value)
            => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount : 0m;

        public static string RightKey(string? treatmentRight)
            => string.IsNullOrWhiteSpace(treatmentRight) ? NO_RIGHT : treatmentRight.Trim();

        /// <summary>
        /// กรองตามสิทธิ์การรักษา (ตรงทั้งคำ ไม่สนช่องว่างหัวท้าย) — ใช้ทั้งหน้าจอและ Export ให้ได้ผลเดียวกัน
        /// สิทธิ์ที่ไม่อยู่ในผลลัพธ์จะไม่ถูกกรอง (แสดงทั้งหมด)
        /// </summary>
        public DischargeQueryResult FilterByRight(string? right)
        {
            var rights = Items
                .GroupBy(i => RightKey(i.TreatmentRight))
                .Select(g => new RightSummary(g.Key, g.Count(), g.Sum(i => ParseAmount(i.Amount))))
                .OrderByDescending(r => r.Amount)
                .ToList();

            var selected = string.IsNullOrWhiteSpace(right) ? null : rights.FirstOrDefault(r => r.Name == right.Trim())?.Name;

            return this with
            {
                Items = selected is null ? Items : Items.Where(i => RightKey(i.TreatmentRight) == selected).ToList(),
                RightFilter = selected,
                Rights = rights,
                UnfilteredCount = Items.Count,
            };
        }
    }

    public sealed record RightSummary(string Name, int Count, decimal Amount);

    public interface IDischargeService
    {
        Task<DischargeQueryResult> GetAsync(string type, DateRange range, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// ดึงข้อมูลผู้ป่วยจำหน่ายจาก HealthObject (10.67.67.64) ผ่าน Stored Procedure
    /// - async ทั้งหมด และยกเลิก query เมื่อผู้ใช้ปิดหน้า
    /// - cache ผลลัพธ์สั้นๆ เพื่อให้ปุ่ม Export ไม่ต้อง query ซ้ำ
    /// </summary>
    public class DischargeService : IDischargeService
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        public const string DEFAULT_IPD_PROCEDURE = "pGetQSHCPatientDischarge";

        /// <summary>OPD ทุกสิทธิ์ (เดิมใช้ pGetQSHCPatientDischargeOPD)</summary>
        public const string DEFAULT_OPD_PROCEDURE = "pGetQSHCPatientDischargeOPDAllPayor";

        private readonly string? _connectionString;
        private readonly int _commandTimeoutSeconds;
        private readonly string _ipdProcedure;
        private readonly string _opdProcedure;
        private readonly IMemoryCache _cache;
        private readonly ILogger<DischargeService> _logger;

        public DischargeService(IConfiguration configuration, IMemoryCache cache, ILogger<DischargeService> logger)
        {
            _connectionString = configuration.GetConnectionString("HealthObject");
            _commandTimeoutSeconds = configuration.GetValue("Discharges:CommandTimeoutSeconds", 180);
            // ชื่อ SP ตั้งค่าได้ (Discharges:IpdProcedure / OpdProcedure) — สลับกลับตัวเดิมได้โดยไม่ต้องแก้โค้ด
            _ipdProcedure = ValidProcedureName(configuration["Discharges:IpdProcedure"], DEFAULT_IPD_PROCEDURE);
            _opdProcedure = ValidProcedureName(configuration["Discharges:OpdProcedure"], DEFAULT_OPD_PROCEDURE);
            _cache = cache;
            _logger = logger;
        }

        public string StoredProcedureFor(string type) => type switch
        {
            PatientTypeCodes.IPD => _ipdProcedure,
            PatientTypeCodes.OPD => _opdProcedure,
            _ => throw new ArgumentOutOfRangeException(nameof(type)),
        };

        /// <summary>ข้อความ error ที่บอกสาเหตุซึ่งแก้ได้ (ไม่พบ SP / คอลัมน์ไม่ตรง / ไม่มีสิทธิ์) แทนข้อความทั่วไป</summary>
        internal static string DescribeError(Exception ex, string procedure) => ex switch
        {
            SqlException { Number: 2812 } => $"ไม่พบ Stored Procedure \"{procedure}\" บนฐาน HealthObject — ตรวจว่าสร้าง SP แล้ว หรือแก้ชื่อที่ appsettings.json (Discharges)",
            SqlException { Number: 229 } => $"บัญชีที่แอปใช้ไม่มีสิทธิ์ EXECUTE \"{procedure}\" — ขอสิทธิ์จากผู้ดูแลฐานข้อมูล",
            SqlException { Number: 201 or 8144 } => $"พารามิเตอร์ของ \"{procedure}\" ไม่ตรง — ต้องรับ @Date_From และ @Date_To",
            IndexOutOfRangeException => $"คอลัมน์ที่ \"{procedure}\" คืนมาไม่ตรงกับที่หน้าจอใช้ (ดูชื่อคอลัมน์ใน log)",
            _ => "ไม่สามารถดึงข้อมูลจากฐานข้อมูลได้ กรุณาลองใหม่อีกครั้ง",
        };

        /// <summary>รับเฉพาะชื่อ SP ที่เป็นตัวอักษร/ตัวเลข/_ (และ schema dbo.) — ค่าอื่นใช้ค่าเริ่มต้น</summary>
        internal static string ValidProcedureName(string? configured, string fallback)
            => !string.IsNullOrWhiteSpace(configured) && System.Text.RegularExpressions.Regex.IsMatch(configured.Trim(), @"^(dbo\.)?[A-Za-z0-9_]+$")
                ? configured.Trim()
                : fallback;

        public async Task<DischargeQueryResult> GetAsync(string type, DateRange range, CancellationToken cancellationToken = default)
        {
            var cacheKey = $"discharge:{StoredProcedureFor(type)}:{range.StartInput}:{range.EndInput}";
            if (_cache.TryGetValue(cacheKey, out DischargeQueryResult? cached) && cached is not null)
            {
                return cached;
            }

            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                _logger.LogError("Connection string 'HealthObject' is not configured");
                return new DischargeQueryResult(type, range, [], "ยังไม่ได้ตั้งค่าการเชื่อมต่อฐานข้อมูล HealthObject");
            }

            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync(cancellationToken);

                await using var cmd = new SqlCommand(StoredProcedureFor(type), conn)
                {
                    CommandType = CommandType.StoredProcedure,
                    CommandTimeout = _commandTimeoutSeconds,
                };
                cmd.Parameters.Add(new SqlParameter("@Date_From", SqlDbType.NVarChar, 30) { Value = range.SqlFrom });
                cmd.Parameters.Add(new SqlParameter("@Date_To", SqlDbType.NVarChar, 30) { Value = range.SqlTo });

                await using var reader = await cmd.ExecuteReaderAsync(CommandBehavior.SequentialAccess, cancellationToken);
                var items = DischargeRecordMapper.ReadAll(reader, type);

                var result = new DischargeQueryResult(type, range, items);
                _cache.Set(cacheKey, result, CacheDuration);
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException or IndexOutOfRangeException)
            {
                _logger.LogError(ex, "Error getting discharge data for {Type} from {Start} to {End}", type, range.StartInput, range.EndInput);
                return new DischargeQueryResult(type, range, [], DescribeError(ex, StoredProcedureFor(type)));
            }
        }
    }
}
