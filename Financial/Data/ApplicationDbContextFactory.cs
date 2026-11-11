using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Financial.Data
{
    /// <summary>
    /// ใช้เฉพาะคำสั่ง dotnet ef (สร้าง migration / script) — ไม่รัน Program.cs จึงไม่เชื่อมต่อหรือ migrate ฐานโดยไม่ตั้งใจ
    /// </summary>
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .Build();

            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlServer(configuration.GetConnectionString("DefaultConnection") ?? "Server=.;Database=QSHC_Financial;Trusted_Connection=True;TrustServerCertificate=True")
                .Options;

            return new ApplicationDbContext(options);
        }
    }
}
