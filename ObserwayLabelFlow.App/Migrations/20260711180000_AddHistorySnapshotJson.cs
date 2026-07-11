using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ObserwayLabelFlow.App.Migrations
{
    /// <inheritdoc />
    public partial class AddHistorySnapshotJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SnapshotJson",
                table: "PrintHistory",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SnapshotJson",
                table: "PrintHistory");
        }
    }
}
