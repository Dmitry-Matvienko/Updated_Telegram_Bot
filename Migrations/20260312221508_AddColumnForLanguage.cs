using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyUpdatedBot.Migrations
{
    /// <inheritdoc />
    public partial class AddColumnForLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "ChatSettings",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "ChatSettings");
        }
    }
}
