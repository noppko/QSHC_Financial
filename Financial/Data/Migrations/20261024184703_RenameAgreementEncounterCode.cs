using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financial.Data.Migrations
{
    /// <inheritdoc />
    public partial class RenameAgreementEncounterCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ตั้งใจลบคอลัมน์เดิม: เดิมเก็บ O/I ที่แปลงแล้ว ตอนนี้เก็บรหัสดิบจาก HIS (OUPAT/INPAT/AEPAT)
            // ค่าใหม่ได้จากการประมวลผลรายงาน สปสช. อีกครั้ง — ข้อมูลส่วนอื่นของตารางไม่ถูกแตะ
            migrationBuilder.DropColumn(
                name: "AgreementPatientTypeCode",
                table: "NhsoFiscalSummaries");

            migrationBuilder.AddColumn<string>(
                name: "AgreementEncounterCode",
                table: "NhsoFiscalSummaries",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgreementEncounterCode",
                table: "NhsoFiscalSummaries");

            migrationBuilder.AddColumn<string>(
                name: "AgreementPatientTypeCode",
                table: "NhsoFiscalSummaries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }
    }
}
