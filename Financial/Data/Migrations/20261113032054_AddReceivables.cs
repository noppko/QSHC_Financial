using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Financial.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReceivables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArReceivables",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HisBillUid = table.Column<long>(type: "bigint", nullable: false),
                    BillNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    HisVisitUid = table.Column<long>(type: "bigint", nullable: false),
                    VisitNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Hn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PatientTypeCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    VisitStartDate = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    DischargeDate = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    BillDate = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    SchemeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PayorCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    PayorName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PayorCategoryCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    AgreementName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    IsOutOfZone = table.Column<bool>(type: "bit", nullable: false),
                    BilledAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaidAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeniedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    WriteOffAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OutstandingAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StatusCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    LastSyncedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArReceivables", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArStatusHistories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceivableId = table.Column<int>(type: "int", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArStatusHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArStatusHistories_ArReceivables_ReceivableId",
                        column: x => x.ReceivableId,
                        principalTable: "ArReceivables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ReferenceValues",
                columns: new[] { "Id", "Category", "Code", "CreatedAt", "CreatedBy", "Description", "DisplayOrder", "IsActive", "IsSystem", "Metadata", "NameEN", "NameTH", "ParentCategory", "ParentId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 24, "AR_STATUS", "OPEN", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", "ดึงจาก HIS ยังไม่ได้ส่งเรียกเก็บ", 10, true, true, null, "Open", "ตั้งหนี้", null, null, null, null },
                    { 25, "AR_STATUS", "CLAIMED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 20, true, true, null, "Claimed", "ส่งเรียกเก็บแล้ว", null, null, null, null },
                    { 26, "AR_STATUS", "PARTIAL", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 30, true, true, null, "Partially paid", "ได้รับบางส่วน", null, null, null, null },
                    { 27, "AR_STATUS", "PAID", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 40, true, true, null, "Paid", "ได้รับครบ", null, null, null, null },
                    { 28, "AR_STATUS", "DENIED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 50, true, true, null, "Denied", "ถูกปฏิเสธ", null, null, null, null },
                    { 29, "AR_STATUS", "APPEAL", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 60, true, true, null, "Appeal", "อุทธรณ์/ส่งใหม่", null, null, null, null },
                    { 30, "AR_STATUS", "WRITE_OFF", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 70, true, true, null, "Write-off", "ตัดหนี้", null, null, null, null },
                    { 31, "AR_STATUS", "CANCELLED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", "บิลถูกยกเลิก/คืนเงินใน HIS หลังตั้งหนี้", 80, true, true, null, "Cancelled", "ยกเลิก", null, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArReceivables_BillDate",
                table: "ArReceivables",
                column: "BillDate");

            migrationBuilder.CreateIndex(
                name: "IX_ArReceivables_HisBillUid",
                table: "ArReceivables",
                column: "HisBillUid",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ArReceivables_SchemeCode_Hn_VisitStartDate",
                table: "ArReceivables",
                columns: new[] { "SchemeCode", "Hn", "VisitStartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ArReceivables_SchemeCode_StatusCode_BillDate",
                table: "ArReceivables",
                columns: new[] { "SchemeCode", "StatusCode", "BillDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ArReceivables_SchemeCode_VisitNumber",
                table: "ArReceivables",
                columns: new[] { "SchemeCode", "VisitNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_ArStatusHistories_ReceivableId",
                table: "ArStatusHistories",
                column: "ReceivableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArStatusHistories");

            migrationBuilder.DropTable(
                name: "ArReceivables");

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 31);
        }
    }
}
