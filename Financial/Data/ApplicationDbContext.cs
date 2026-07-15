using Financial.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Financial.Data
{
    /// <summary>
    /// DbContext หลักของระบบ รองรับ Identity และ Audit Fields
    /// </summary>
    public class ApplicationDbContext : IdentityDbContext
    {
        private readonly IHttpContextAccessor? _httpContextAccessor;

        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options,
            IHttpContextAccessor? httpContextAccessor = null)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        // DbSets
        public DbSet<ReferenceValue> ReferenceValues { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // กำหนด Unique Composite Index สำหรับ ReferenceValue (Category + Code)
            modelBuilder.Entity<ReferenceValue>()
                .HasIndex(rv => new { rv.Category, rv.Code })
                .IsUnique()
                .HasFilter("[IsActive] = 1"); // Unique เฉพาะ records ที่ IsActive = true

            // Global Query Filter สำหรับ Soft Delete - กรองเฉพาะ IsActive = true
            modelBuilder.Entity<ReferenceValue>()
                .HasQueryFilter(e => e.IsActive);

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
                    entry.Entity.CreatedAt = DateTime.Now; // ใช้เวลาท้องถิ่น
                    entry.Entity.CreatedBy = currentUser;
                    entry.Entity.IsActive = true;
                }
                else if (entry.State == EntityState.Modified)
                {
                    // แก้ไข record
                    entry.Entity.UpdatedAt = DateTime.Now; // ใช้เวลาท้องถิ่น
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
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
                    CreatedAt = DateTime.Now
                }
            };

            modelBuilder.Entity<ReferenceValue>().HasData(referenceValues);
        }
    }
}