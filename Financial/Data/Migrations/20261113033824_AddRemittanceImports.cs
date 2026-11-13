using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Financial.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddRemittanceImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ArImportBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SchemeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    StatementNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransferDate = table.Column<DateTime>(type: "date", nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    FileSha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    TotalReceived = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    StatusCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConfirmedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CancelledBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CancelledAt = table.Column<DateTime>(type: "datetime2(0)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArImportBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReceivableId = table.Column<int>(type: "int", nullable: false),
                    ImportBatchId = table.Column<int>(type: "int", nullable: true),
                    ImportRowId = table.Column<int>(type: "int", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeniedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReasonText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TransferDate = table.Column<DateTime>(type: "date", nullable: true),
                    StatementNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SourceCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArPayments_ArReceivables_ReceivableId",
                        column: x => x.ReceivableId,
                        principalTable: "ArReceivables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ArImportRows",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ImportBatchId = table.Column<int>(type: "int", nullable: false),
                    RowNo = table.Column<int>(type: "int", nullable: false),
                    VisitNumber = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Hn = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    VisitDate = table.Column<DateTime>(type: "date", nullable: true),
                    DischargeDate = table.Column<DateTime>(type: "date", nullable: true),
                    BillNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ClaimedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ReceivedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DeniedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ReasonText = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    StatementNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransferDate = table.Column<DateTime>(type: "date", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    MatchResultCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MatchStep = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    ReceivableId = table.Column<int>(type: "int", nullable: true),
                    CandidateIds = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsAccepted = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArImportRows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArImportRows_ArImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "ArImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArImportRows_ArReceivables_ReceivableId",
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
                    { 32, "AR_MATCH_RESULT", "MATCHED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 10, true, true, null, "MATCHED", "จับคู่ได้", null, null, null, null },
                    { 33, "AR_MATCH_RESULT", "AMOUNT_DIFF", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 20, true, true, null, "AMOUNT_DIFF", "ยอดต่าง", null, null, null, null },
                    { 34, "AR_MATCH_RESULT", "DENIED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 30, true, true, null, "DENIED", "ปฏิเสธ", null, null, null, null },
                    { 35, "AR_MATCH_RESULT", "SUGGESTED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 40, true, true, null, "SUGGESTED", "แนะนำ (ต้องยืนยัน)", null, null, null, null },
                    { 36, "AR_MATCH_RESULT", "MULTIPLE", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 50, true, true, null, "MULTIPLE", "พบหลายรายการ", null, null, null, null },
                    { 37, "AR_MATCH_RESULT", "NOT_FOUND", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 60, true, true, null, "NOT_FOUND", "ไม่พบ", null, null, null, null },
                    { 38, "AR_MATCH_RESULT", "DUPLICATE", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 70, true, true, null, "DUPLICATE", "ซ้ำ", null, null, null, null },
                    { 39, "AR_MATCH_RESULT", "INVALID", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 80, true, true, null, "INVALID", "ข้อมูลไม่ถูกต้อง", null, null, null, null },
                    { 40, "AR_IMPORT_STATUS", "VALIDATED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 10, true, true, null, "VALIDATED", "รอยืนยัน", null, null, null, null },
                    { 41, "AR_IMPORT_STATUS", "CONFIRMED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 20, true, true, null, "CONFIRMED", "ยืนยันแล้ว", null, null, null, null },
                    { 42, "AR_IMPORT_STATUS", "CANCELLED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 30, true, true, null, "CANCELLED", "ยกเลิก", null, null, null, null },
                    { 43, "AR_PAYMENT_SOURCE", "IMPORT", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 10, true, true, null, "Import", "นำเข้าไฟล์ผลการเรียกเก็บ", null, null, null, null },
                    { 44, "AR_PAYMENT_SOURCE", "MANUAL", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 20, true, true, null, "Manual", "บันทึกเอง", null, null, null, null },
                    { 45, "AR_PAYMENT_SOURCE", "WRITE_OFF", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 30, true, true, null, "Write-off", "ตัดหนี้", null, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArImportBatches_FileSha256",
                table: "ArImportBatches",
                column: "FileSha256");

            migrationBuilder.CreateIndex(
                name: "IX_ArImportBatches_SchemeCode_StatusCode",
                table: "ArImportBatches",
                columns: new[] { "SchemeCode", "StatusCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ArImportRows_ImportBatchId_MatchResultCode",
                table: "ArImportRows",
                columns: new[] { "ImportBatchId", "MatchResultCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ArImportRows_ReceivableId",
                table: "ArImportRows",
                column: "ReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_ArPayments_ImportBatchId",
                table: "ArPayments",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_ArPayments_ReceivableId",
                table: "ArPayments",
                column: "ReceivableId");

            migrationBuilder.CreateIndex(
                name: "IX_ArPayments_StatementNo_ReceivableId",
                table: "ArPayments",
                columns: new[] { "StatementNo", "ReceivableId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArImportRows");

            migrationBuilder.DropTable(
                name: "ArPayments");

            migrationBuilder.DropTable(
                name: "ArImportBatches");

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 45);
        }
    }
}
