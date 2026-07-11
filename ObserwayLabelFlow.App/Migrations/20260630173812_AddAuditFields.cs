using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObserwayLabelFlow.App.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Copies",
                table: "PrintHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ErrorMessage",
                table: "PrintHistory",
                type: "TEXT",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaperSize",
                table: "PrintHistory",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrintedBy",
                table: "PrintHistory",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrinterName",
                table: "PrintHistory",
                type: "TEXT",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Success",
                table: "PrintHistory",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PrintHistory_Success",
                table: "PrintHistory",
                column: "Success");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PrintHistory_Success",
                table: "PrintHistory");

            migrationBuilder.DropColumn(
                name: "Copies",
                table: "PrintHistory");

            migrationBuilder.DropColumn(
                name: "ErrorMessage",
                table: "PrintHistory");

            migrationBuilder.DropColumn(
                name: "PaperSize",
                table: "PrintHistory");

            migrationBuilder.DropColumn(
                name: "PrintedBy",
                table: "PrintHistory");

            migrationBuilder.DropColumn(
                name: "PrinterName",
                table: "PrintHistory");

            migrationBuilder.DropColumn(
                name: "Success",
                table: "PrintHistory");
        }
    }
}
