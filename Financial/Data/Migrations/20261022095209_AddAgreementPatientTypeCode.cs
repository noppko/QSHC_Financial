using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Financial.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAgreementPatientTypeCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AgreementPatientTypeCode",
                table: "NhsoFiscalSummaries",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AgreementPatientTypeCode",
                table: "NhsoFiscalSummaries");
        }
    }
}
