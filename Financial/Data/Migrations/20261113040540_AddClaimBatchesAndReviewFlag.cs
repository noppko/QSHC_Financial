using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Financial.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimBatchesAndReviewFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewFlaggedAt",
                table: "ArReceivables",
                type: "datetime2(0)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewReason",
                table: "ArReceivables",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ClaimBatchId",
                table: "ArImportBatches",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArClaimBatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    BatchNo = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    SchemeCode = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    PatientTypeCode = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    PayorFilter = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PeriodFrom = table.Column<DateTime>(type: "date", nullable: true),
                    PeriodTo = table.Column<DateTime>(type: "date", nullable: true),
                    StatusCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SentDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReferenceDocNo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SentBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ItemCount = table.Column<int>(type: "int", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArClaimBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ArClaimBatchItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimBatchId = table.Column<int>(type: "int", nullable: false),
                    ReceivableId = table.Column<int>(type: "int", nullable: false),
                    ClaimedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArClaimBatchItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ArClaimBatchItems_ArClaimBatches_ClaimBatchId",
                        column: x => x.ClaimBatchId,
                        principalTable: "ArClaimBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ArClaimBatchItems_ArReceivables_ReceivableId",
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
                    { 46, "AR_CLAIM_BATCH_STATUS", "DRAFT", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 10, true, true, null, "DRAFT", "จัดชุด (ยังไม่ส่ง)", null, null, null, null },
                    { 47, "AR_CLAIM_BATCH_STATUS", "SENT", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 20, true, true, null, "SENT", "ส่งแล้ว", null, null, null, null },
                    { 48, "AR_CLAIM_BATCH_STATUS", "CANCELLED", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", null, 30, true, true, null, "CANCELLED", "ยกเลิก", null, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ArClaimBatches_BatchNo",
                table: "ArClaimBatches",
                column: "BatchNo",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ArClaimBatches_SchemeCode_StatusCode",
                table: "ArClaimBatches",
                columns: new[] { "SchemeCode", "StatusCode" });

            migrationBuilder.CreateIndex(
                name: "IX_ArClaimBatchItems_ClaimBatchId_ReceivableId",
                table: "ArClaimBatchItems",
                columns: new[] { "ClaimBatchId", "ReceivableId" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ArClaimBatchItems_ReceivableId",
                table: "ArClaimBatchItems",
                column: "ReceivableId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArClaimBatchItems");

            migrationBuilder.DropTable(
                name: "ArClaimBatches");

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DropColumn(
                name: "ReviewFlaggedAt",
                table: "ArReceivables");

            migrationBuilder.DropColumn(
                name: "ReviewReason",
                table: "ArReceivables");

            migrationBuilder.DropColumn(
                name: "ClaimBatchId",
                table: "ArImportBatches");
        }
    }
}
