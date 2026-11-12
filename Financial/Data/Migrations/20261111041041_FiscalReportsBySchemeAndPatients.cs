using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Financial.Data.Migrations
{
    /// <inheritdoc />
    public partial class FiscalReportsBySchemeAndPatients : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NhsoFiscalSummaries");

            migrationBuilder.CreateTable(
                name: "FiscalPayorSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    SchemeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PatientTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    VisitEncounterCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    MovedFromBillSide = table.Column<bool>(type: "bit", nullable: false),
                    PayorCategoryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AgreementName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Patients = table.Column<int>(type: "int", nullable: false),
                    Visits = table.Column<int>(type: "int", nullable: false),
                    Bills = table.Column<int>(type: "int", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefreshedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalPayorSummaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FiscalSchemeTotals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    SchemeCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    PatientTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ZoneCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Patients = table.Column<int>(type: "int", nullable: false),
                    Visits = table.Column<int>(type: "int", nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RefreshedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalSchemeTotals", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ReferenceValues",
                columns: new[] { "Id", "Category", "Code", "CreatedAt", "CreatedBy", "Description", "DisplayOrder", "IsActive", "IsSystem", "Metadata", "NameEN", "NameTH", "ParentCategory", "ParentId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 17, "PAYOR_SCHEME", "NHSO", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", "หลักประกันสุขภาพถ้วนหน้า", 10, true, true, null, "NHSO (UCS)", "สปสช.", null, null, null, null },
                    { 18, "PAYOR_SCHEME", "CSMBS", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", "สวัสดิการรักษาพยาบาลข้าราชการ", 20, true, true, null, "CSMBS", "กรมบัญชีกลาง", null, null, null, null },
                    { 19, "PAYOR_SCHEME", "SSO", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", "สำนักงานประกันสังคม", 30, true, true, null, "SSO", "ประกันสังคม", null, null, null, null },
                    { 20, "FISCAL_ZONE", "IN", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 10, true, true, null, "In zone", "ในเขต", null, null, null, null },
                    { 21, "FISCAL_ZONE", "OUT", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 20, true, true, null, "Out of zone", "นอกเขต", null, null, null, null },
                    { 22, "FISCAL_ZONE", "ALL", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 30, true, true, null, "Total", "รวม", null, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPayorSummaries_FiscalYear_SchemeCode_PatientTypeCode",
                table: "FiscalPayorSummaries",
                columns: new[] { "FiscalYear", "SchemeCode", "PatientTypeCode" });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalSchemeTotals_FiscalYear_SchemeCode_PatientTypeCode_ZoneCode",
                table: "FiscalSchemeTotals",
                columns: new[] { "FiscalYear", "SchemeCode", "PatientTypeCode", "ZoneCode" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalPayorSummaries");

            migrationBuilder.DropTable(
                name: "FiscalSchemeTotals");

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.CreateTable(
                name: "NhsoFiscalSummaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AgreementEncounterCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    AgreementName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    Bills = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PatientTypeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PayorCategoryCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RefreshedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Visits = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhsoFiscalSummaries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NhsoFiscalSummaries_FiscalYear_PatientTypeCode",
                table: "NhsoFiscalSummaries",
                columns: new[] { "FiscalYear", "PatientTypeCode" });
        }
    }
}
