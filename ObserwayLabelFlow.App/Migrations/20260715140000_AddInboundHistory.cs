using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObserwayLabelFlow.App.Migrations
{
    /// <inheritdoc />
    public partial class AddInboundHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "InboundHistory",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Reference = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OrderNumber = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Success = table.Column<bool>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: true),
                    MarkedBy = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboundHistory", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboundHistory_CreatedAtUtc",
                table: "InboundHistory",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_InboundHistory_Success",
                table: "InboundHistory",
                column: "Success");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "InboundHistory");
        }
    }
}
