using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hexara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMoveVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "Version",
                table: "GameMoves",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "GameMoves");
        }
    }
}
