using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Financial.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialWithReferenceValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferenceValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    NameTH = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    NameEN = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    ParentCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ParentId = table.Column<int>(type: "int", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferenceValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferenceValues_ReferenceValues_ParentId",
                        column: x => x.ParentId,
                        principalTable: "ReferenceValues",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ReferenceValues",
                columns: new[] { "Id", "Category", "Code", "CreatedAt", "CreatedBy", "Description", "DisplayOrder", "IsActive", "IsSystem", "Metadata", "NameEN", "NameTH", "ParentCategory", "ParentId", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "TITLE", "MR", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 1, true, true, null, "Mr.", "นาย", null, null, null, null },
                    { 2, "TITLE", "MRS", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 2, true, true, null, "Mrs.", "นาง", null, null, null, null },
                    { 3, "TITLE", "MISS", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 3, true, true, null, "Miss", "นางสาว", null, null, null, null },
                    { 4, "TITLE", "DR", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 4, true, true, null, "Dr.", "นายแพทย์/แพทย์หญิง", null, null, null, null },
                    { 5, "GENDER", "MALE", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 1, true, true, null, "Male", "ชาย", null, null, null, null },
                    { 6, "GENDER", "FEMALE", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 2, true, true, null, "Female", "หญิง", null, null, null, null },
                    { 7, "GENDER", "OTHER", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 3, true, true, null, "Other", "อื่นๆ", null, null, null, null },
                    { 8, "BLOOD_TYPE", "A", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 1, true, true, null, "A", "A", null, null, null, null },
                    { 9, "BLOOD_TYPE", "B", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 2, true, true, null, "B", "B", null, null, null, null },
                    { 10, "BLOOD_TYPE", "AB", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 3, true, true, null, "AB", "AB", null, null, null, null },
                    { 11, "BLOOD_TYPE", "O", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "System", null, 4, true, true, null, "O", "O", null, null, null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceValues_Category_Code",
                table: "ReferenceValues",
                columns: new[] { "Category", "Code" },
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ReferenceValues_ParentId",
                table: "ReferenceValues",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferenceValues");
        }
    }
}
