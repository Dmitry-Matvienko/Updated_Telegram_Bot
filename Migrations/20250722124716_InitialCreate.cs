using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyUpdatedBot.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<long>(type: "bigint", nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageStats",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserRefId = table.Column<long>(type: "bigint", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Rank = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MessageCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageStats_Users_UserRefId",
                        column: x => x.UserRefId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageStats_UserRefId",
                table: "MessageStats",
                column: "UserRefId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserId",
                table: "Users",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageStats");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
