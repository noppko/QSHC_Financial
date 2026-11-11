using Financial.Models.Dashboard;
using Financial.Services.Dashboard;
using FluentAssertions;
using System.Data;
using System.Text.Json;

namespace Financial.Tests.Services
{
    /// <summary>HN/ชื่อ-สกุลผู้ป่วย (PHI) — เฉพาะ Admin เท่านั้น</summary>
    public class PatientIdentityTests
    {
        private static DataTable IdentityTable(params object[][] rows)
        {
            var table = new DataTable();
            foreach (var c in new[] { "VisitUid", "Hn", "Title", "FirstName", "LastName", "Anon" })
                table.Columns.Add(c, c == "VisitUid" ? typeof(long) : typeof(string));
            foreach (var r in rows) table.Rows.Add(r);
            return table;
        }

        [Fact]
        public void ReadIdentities_CombinesTitleAndName_AndTrims()
        {
            using var reader = IdentityTable(new object[] { 100L, " 69000123 ", "นาย", "สมชาย ", " ใจดี", "N" }).CreateDataReader();

            var map = HisFinanceDashboardService.ReadIdentities(reader);

            map[100L].Should().Be(new PatientIdentity("69000123", "นาย สมชาย ใจดี"));
        }

        [Fact]
        public void ReadIdentities_AnonymousPatient_HidesNameAndHn()
        {
            using var reader = IdentityTable(new object[] { 7L, "69000999", "นาง", "ลับ", "มาก", "Y" }).CreateDataReader();

            var map = HisFinanceDashboardService.ReadIdentities(reader);

            map[7L].Should().Be(PatientIdentity.Anonymous);
            map[7L].Hn.Should().BeNull();
            map[7L].Name.Should().Be("ผู้ป่วยปกปิดชื่อ");
        }

        [Fact]
        public void ReadIdentities_MissingValues_AreNull()
        {
            using var reader = IdentityTable(new object[] { 1L, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value }).CreateDataReader();

            HisFinanceDashboardService.ReadIdentities(reader)[1L].Should().Be(new PatientIdentity(null, null));
        }

        [Fact]
        public void VisitUid_IsNeverSerializedToTheBrowser()
        {
            var p = new PendingPayment("O", "#0001", "F", "เงินสด", "คลินิก", 100m, 5, true, VisitUid: 123456789012L, Hn: "69000001", PatientName: "ทดสอบ");

            var json = JsonSerializer.Serialize(p);

            json.Should().NotContain("123456789012").And.NotContain("VisitUid");
            using var doc = JsonDocument.Parse(json);
            doc.RootElement.GetProperty("Hn").GetString().Should().Be("69000001");           // Hn/PatientName ถูกส่งเฉพาะเมื่อ server ใส่ให้ (Admin)
            doc.RootElement.GetProperty("PatientName").GetString().Should().Be("ทดสอบ");
        }

        [Fact]
        public async Task SampleService_IdentityOnlyWhenRequested_AndClearlyFake()
        {
            var service = new SampleFinanceDashboardService(TimeProvider.System);

            var normal = await service.GetAsync();
            var admin = await service.GetAsync(includePatientIdentity: true);

            normal.PendingPayments.Should().OnlyContain(p => p.Hn == null && p.PatientName == null);
            admin.PendingPayments.Should().OnlyContain(p => p.Hn != null && p.PatientName!.StartsWith("(ตัวอย่าง)"));
        }
    }
}
