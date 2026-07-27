using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hexara.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRoomBoardCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BoardCode",
                table: "Rooms",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoardCode",
                table: "Rooms");
        }
    }
}
