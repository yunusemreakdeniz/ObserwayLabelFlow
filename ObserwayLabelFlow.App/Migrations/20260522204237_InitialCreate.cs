using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObserwayLabelFlow.App.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PrintHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TrackingNumber = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    PdfUrl = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CustomerName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    OrderStatus = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    CarrierName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    ProductCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrintHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrintHistory_CreatedAtUtc",
                table: "PrintHistory",
                column: "CreatedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PrintHistory");
        }
    }
}
