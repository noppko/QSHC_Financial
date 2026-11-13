using Financial.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace Financial.Data
{
    /// <summary>
    /// DbContext หลักของระบบ (QSHC_Financial) พร้อม Audit Fields — ไม่ใช้ ASP.NET Identity (ยืนยันตัวตนผ่าน External Auth API)
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // DbSets (ฐาน QSHC_Financial — ข้อมูลของระบบ Financial แยกจาก HIS)
        public DbSet<ReferenceValue> ReferenceValues { get; set; } = null!;
        public DbSet<AppSetting> AppSettings { get; set; } = null!;
        public DbSet<FiscalPayorSummary> FiscalPayorSummaries { get; set; } = null!;
        public DbSet<FiscalSchemeTotal> FiscalSchemeTotals { get; set; } = null!;
        public DbSet<Models.Receivables.ArReceivable> ArReceivables { get; set; } = null!;
        public DbSet<Models.Receivables.ArStatusHistory> ArStatusHistories { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Global Query Filter สำหรับ Soft Delete ของทุก Entity ที่ inherit BaseEntity - กรองเฉพาะ IsActive = true
            foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t.ClrType)))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var body = Expression.Property(parameter, nameof(BaseEntity.IsActive));
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(Expression.Lambda(body, parameter));
                modelBuilder.Entity(entityType.ClrType).Property(nameof(BaseEntity.IsActive)).HasDefaultValue(true);
            }

            // กำหนด Unique Composite Index สำหรับ ReferenceValue (Category + Code)
            modelBuilder.Entity<ReferenceValue>()
                .HasIndex(rv => new { rv.Category, rv.Code })
                .IsUnique()
                .HasFilter("[IsActive] = 1"); // Unique เฉพาะ records ที่ IsActive = true

            modelBuilder.Entity<AppSetting>()
                .HasIndex(s => s.Key)
                .IsUnique()
                .HasFilter("[IsActive] = 1");

            modelBuilder.Entity<FiscalPayorSummary>()
                .HasIndex(s => new { s.FiscalYear, s.SchemeCode, s.PatientTypeCode });

            modelBuilder.Entity<FiscalSchemeTotal>()
                .HasIndex(s => new { s.FiscalYear, s.SchemeCode, s.PatientTypeCode, s.ZoneCode });

            // ลูกหนี้สิทธิ: 1 บิล HIS = 1 แถว · index สำหรับภาพรวม/จับคู่ (VN/AN, HN + วันที่เปิด visit)
            modelBuilder.Entity<Models.Receivables.ArReceivable>(e =>
            {
                e.HasIndex(r => r.HisBillUid).IsUnique().HasFilter("[IsActive] = 1");
                e.HasIndex(r => new { r.SchemeCode, r.StatusCode, r.BillDate });
                e.HasIndex(r => new { r.SchemeCode, r.VisitNumber });
                e.HasIndex(r => new { r.SchemeCode, r.Hn, r.VisitStartDate });
                e.HasIndex(r => r.BillDate);
            });
            modelBuilder.Entity<Models.Receivables.ArStatusHistory>()
                .HasOne(h => h.Receivable).WithMany(r => r.StatusHistories)
                .HasForeignKey(h => h.ReceivableId).OnDelete(DeleteBehavior.Restrict);

            // Self-referencing relationship สำหรับ ReferenceValue
            modelBuilder.Entity<ReferenceValue>()
                .HasOne(rv => rv.Parent)
                .WithMany(rv => rv.Children)
                .HasForeignKey(rv => rv.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Seed data สำหรับ ReferenceValues
            SeedReferenceValues(modelBuilder);
        }

        /// <summary>
        /// Override SaveChangesAsync เพื่อตั้งค่า Audit Fields อัตโนมัติ
        /// </summary>
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            // ดึงชื่อผู้ใช้ปัจจุบันจาก HttpContext
            var currentUser = _httpContextAccessor?.HttpContext?.User?.Identity?.Name ?? "System";

            var entries = ChangeTracker.Entries<BaseEntity>();

            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    // เพิ่ม record ใหม่
                    entry.Entity.CreatedAt = DateTime.Now; // เวลาไทย (ห้ามใช้ UtcNow)
                    entry.Entity.CreatedBy = currentUser;
                    entry.Entity.IsActive = true;
                }
                else if (entry.State == EntityState.Modified)
                {
                    // แก้ไข record
                    entry.Entity.UpdatedAt = DateTime.Now; // เวลาไทย (ห้ามใช้ UtcNow)
                    entry.Entity.UpdatedBy = currentUser;

                    // ป้องกันไม่ให้แก้ไข CreatedAt และ CreatedBy
                    entry.Property(e => e.CreatedAt).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                }
            }

            return await base.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// Seed ข้อมูล Reference Values เริ่มต้น
        /// </summary>
        private void SeedReferenceValues(ModelBuilder modelBuilder)
        {
            // ใช้ static datetime เพื่อป้องกัน model changes warning
            var seedDate = new DateTime(2025, 1, 1, 0, 0, 0);

            var referenceValues = new List<ReferenceValue>
            {
                // TITLE - คำนำหน้าชื่อ
                new ReferenceValue
                {
                    Id = 1,
                    Category = "TITLE",
                    Code = "MR",
                    NameTH = "นาย",
                    NameEN = "Mr.",
                    DisplayOrder = 1,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },
                new ReferenceValue
                {
                    Id = 2,
                    Category = "TITLE",
                    Code = "MRS",
                    NameTH = "นาง",
                    NameEN = "Mrs.",
                    DisplayOrder = 2,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },
                new ReferenceValue
                {
                    Id = 3,
                    Category = "TITLE",
                    Code = "MISS",
                    NameTH = "นางสาว",
                    NameEN = "Miss",
                    DisplayOrder = 3,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },
                new ReferenceValue
                {
                    Id = 4,
                    Category = "TITLE",
                    Code = "DR",
                    NameTH = "นายแพทย์/แพทย์หญิง",
                    NameEN = "Dr.",
                    DisplayOrder = 4,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },

                // GENDER - เพศ
                new ReferenceValue
                {
                    Id = 5,
                    Category = "GENDER",
                    Code = "MALE",
                    NameTH = "ชาย",
                    NameEN = "Male",
                    DisplayOrder = 1,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },
                new ReferenceValue
                {
                    Id = 6,
                    Category = "GENDER",
                    Code = "FEMALE",
                    NameTH = "หญิง",
                    NameEN = "Female",
                    DisplayOrder = 2,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },
                new ReferenceValue
                {
                    Id = 7,
                    Category = "GENDER",
                    Code = "OTHER",
                    NameTH = "อื่นๆ",
                    NameEN = "Other",
                    DisplayOrder = 3,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },

                // BLOOD_TYPE - กรุ๊ปเลือด
                new ReferenceValue
                {
                    Id = 8,
                    Category = "BLOOD_TYPE",
                    Code = "A",
                    NameTH = "A",
                    NameEN = "A",
                    DisplayOrder = 1,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },
                new ReferenceValue
                {
                    Id = 9,
                    Category = "BLOOD_TYPE",
                    Code = "B",
                    NameTH = "B",
                    NameEN = "B",
                    DisplayOrder = 2,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },
                new ReferenceValue
                {
                    Id = 10,
                    Category = "BLOOD_TYPE",
                    Code = "AB",
                    NameTH = "AB",
                    NameEN = "AB",
                    DisplayOrder = 3,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },
                new ReferenceValue
                {
                    Id = 11,
                    Category = "BLOOD_TYPE",
                    Code = "O",
                    NameTH = "O",
                    NameEN = "O",
                    DisplayOrder = 4,
                    IsSystem = true,
                    CreatedBy = "System",
                    CreatedAt = seedDate
                },

                // PATIENT_TYPE - ประเภทผู้ป่วย (ใช้แทน enum; รหัสตรงกับฝั่งบิลใน HIS)
                new ReferenceValue { Id = 12, Category = ReferenceCategories.PATIENT_TYPE, Code = PatientTypeCodes.OPD, NameTH = "ผู้ป่วยนอก", NameEN = "OPD", DisplayOrder = 1, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 13, Category = ReferenceCategories.PATIENT_TYPE, Code = PatientTypeCodes.IPD, NameTH = "ผู้ป่วยใน", NameEN = "IPD", DisplayOrder = 2, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },

                // ACCESS_LEVEL - ระดับสิทธิ์การใช้งาน (ใช้แทน enum)
                new ReferenceValue { Id = 14, Category = ReferenceCategories.ACCESS_LEVEL, Code = AccessLevelCodes.ADMIN, NameTH = "ผู้ดูแลระบบ", NameEN = "Administrator", Description = "ตั้งค่าระบบได้ และดูได้ทุกหน้า", DisplayOrder = 1, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 15, Category = ReferenceCategories.ACCESS_LEVEL, Code = AccessLevelCodes.FINANCE, NameTH = "เจ้าหน้าที่การเงิน", NameEN = "Finance", Description = "role ขึ้นต้นด้วย Cashier — เข้าเมนูการเงินได้", DisplayOrder = 2, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 16, Category = ReferenceCategories.ACCESS_LEVEL, Code = AccessLevelCodes.GENERAL, NameTH = "ผู้ใช้งานทั่วไป (Dashboard)", NameEN = "General", Description = "ดูได้เฉพาะ Dashboard", DisplayOrder = 3, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },

                // สิทธิ์ที่มีรายงานตามปีงบประมาณ (กลุ่มผู้จ่ายใน HIS ตั้งค่าที่ Reports:Fiscal:Schemes)
                new ReferenceValue { Id = 17, Category = ReferenceCategories.PAYOR_SCHEME, Code = PayorSchemeCodes.NHSO, NameTH = "สปสช.", NameEN = "NHSO (UCS)", Description = "หลักประกันสุขภาพถ้วนหน้า", DisplayOrder = 10, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 18, Category = ReferenceCategories.PAYOR_SCHEME, Code = PayorSchemeCodes.CSMBS, NameTH = "กรมบัญชีกลาง", NameEN = "CSMBS", Description = "สวัสดิการรักษาพยาบาลข้าราชการ", DisplayOrder = 20, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 19, Category = ReferenceCategories.PAYOR_SCHEME, Code = PayorSchemeCodes.SSO, NameTH = "ประกันสังคม", NameEN = "SSO", Description = "สำนักงานประกันสังคม", DisplayOrder = 30, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },

                // เขตของเงื่อนไขการเรียกเก็บ (นอกเขต = ชื่อเงื่อนไขมีคำว่า "นอก")
                new ReferenceValue { Id = 23, Category = ReferenceCategories.PAYOR_SCHEME, Code = PayorSchemeCodes.LGO, NameTH = "อปท.", NameEN = "LGO", Description = "องค์กรปกครองส่วนท้องถิ่น", DisplayOrder = 40, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },

                new ReferenceValue { Id = 20, Category = ReferenceCategories.FISCAL_ZONE, Code = FiscalZoneCodes.IN, NameTH = "ในเขต", NameEN = "In zone", DisplayOrder = 10, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 21, Category = ReferenceCategories.FISCAL_ZONE, Code = FiscalZoneCodes.OUT, NameTH = "นอกเขต", NameEN = "Out of zone", DisplayOrder = 20, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 22, Category = ReferenceCategories.FISCAL_ZONE, Code = FiscalZoneCodes.ALL, NameTH = "รวม", NameEN = "Total", DisplayOrder = 30, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },

                // AR_STATUS - สถานะลูกหนี้สิทธิ
                new ReferenceValue { Id = 24, Category = ReferenceCategories.AR_STATUS, Code = ArStatusCodes.OPEN, NameTH = "ตั้งหนี้", NameEN = "Open", Description = "ดึงจาก HIS ยังไม่ได้ส่งเรียกเก็บ", DisplayOrder = 10, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 25, Category = ReferenceCategories.AR_STATUS, Code = ArStatusCodes.CLAIMED, NameTH = "ส่งเรียกเก็บแล้ว", NameEN = "Claimed", DisplayOrder = 20, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 26, Category = ReferenceCategories.AR_STATUS, Code = ArStatusCodes.PARTIAL, NameTH = "ได้รับบางส่วน", NameEN = "Partially paid", DisplayOrder = 30, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 27, Category = ReferenceCategories.AR_STATUS, Code = ArStatusCodes.PAID, NameTH = "ได้รับครบ", NameEN = "Paid", DisplayOrder = 40, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 28, Category = ReferenceCategories.AR_STATUS, Code = ArStatusCodes.DENIED, NameTH = "ถูกปฏิเสธ", NameEN = "Denied", DisplayOrder = 50, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 29, Category = ReferenceCategories.AR_STATUS, Code = ArStatusCodes.APPEAL, NameTH = "อุทธรณ์/ส่งใหม่", NameEN = "Appeal", DisplayOrder = 60, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 30, Category = ReferenceCategories.AR_STATUS, Code = ArStatusCodes.WRITE_OFF, NameTH = "ตัดหนี้", NameEN = "Write-off", DisplayOrder = 70, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
                new ReferenceValue { Id = 31, Category = ReferenceCategories.AR_STATUS, Code = ArStatusCodes.CANCELLED, NameTH = "ยกเลิก", NameEN = "Cancelled", Description = "บิลถูกยกเลิก/คืนเงินใน HIS หลังตั้งหนี้", DisplayOrder = 80, IsSystem = true, CreatedBy = "System", CreatedAt = seedDate },
            };

            modelBuilder.Entity<ReferenceValue>().HasData(referenceValues);
        }
    }
}