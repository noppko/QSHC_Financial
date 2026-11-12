using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financial.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLgoPayorScheme : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "ReferenceValues",
                columns: new[] { "Id", "Category", "Code", "CreatedAt", "CreatedBy", "Description", "DisplayOrder", "IsActive", "IsSystem", "Metadata", "NameEN", "NameTH", "ParentCategory", "ParentId", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 23, "PAYOR_SCHEME", "LGO", new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), "System", "องค์กรปกครองส่วนท้องถิ่น", 40, true, true, null, "LGO", "อปท.", null, null, null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "ReferenceValues",
                keyColumn: "Id",
                keyValue: 23);
        }
    }
}
