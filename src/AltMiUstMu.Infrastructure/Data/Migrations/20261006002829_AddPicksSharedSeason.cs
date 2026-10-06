using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AltMiUstMu.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPicksSharedSeason : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PicksSharedSeasonId",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PicksSharedSeasonId",
                table: "AspNetUsers");
        }
    }
}
