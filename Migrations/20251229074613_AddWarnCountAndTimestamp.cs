using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyUpdatedBot.Migrations
{
    /// <inheritdoc />
    public partial class AddWarnCountAndTimestamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WarningRecords_UserRefId_ChatId_CreatedAtUtc",
                table: "WarningRecords");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "WarningRecords",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarningsCount",
                table: "WarningRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_WarningRecords_UserRefId_ChatId",
                table: "WarningRecords",
                columns: new[] { "UserRefId", "ChatId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WarningRecords_UserRefId_ChatId",
                table: "WarningRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "WarningRecords");

            migrationBuilder.DropColumn(
                name: "WarningsCount",
                table: "WarningRecords");

            migrationBuilder.CreateIndex(
                name: "IX_WarningRecords_UserRefId_ChatId_CreatedAtUtc",
                table: "WarningRecords",
                columns: new[] { "UserRefId", "ChatId", "CreatedAtUtc" });
        }
    }
}
