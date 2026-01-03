using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyUpdatedBot.Migrations
{
    /// <inheritdoc />
    public partial class AddWarningRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WarningRecords",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    UserRefId = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarningRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WarningRecords_Users_UserRefId",
                        column: x => x.UserRefId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WarningRecords_UserRefId_ChatId_CreatedAtUtc",
                table: "WarningRecords",
                columns: new[] { "UserRefId", "ChatId", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarningRecords");
        }
    }
}
