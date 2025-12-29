using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyUpdatedBot.Migrations
{
    /// <inheritdoc />
    public partial class AddNewEntityAndDropColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
        "IF OBJECT_ID(N'dbo.RatingEntity', N'U') IS NOT NULL DROP TABLE dbo.RatingEntity;");


            migrationBuilder.CreateTable(
                name: "RatingStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserRefId = table.Column<long>(type: "bigint", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingStats", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingStats_Users_UserRefId",
                        column: x => x.UserRefId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ReputationGivens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FromUserId = table.Column<long>(type: "bigint", nullable: false),
                    ToUserRefId = table.Column<long>(type: "bigint", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    LastGiven = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReputationGivens", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RatingStats_UserRefId_ChatId",
                table: "RatingStats",
                columns: new[] { "UserRefId", "ChatId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReputationGivens_FromUserId_ToUserRefId_ChatId",
                table: "ReputationGivens",
                columns: new[] { "FromUserId", "ToUserRefId", "ChatId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RatingStats");

            migrationBuilder.DropTable(
                name: "ReputationGivens");

            migrationBuilder.CreateTable(
                name: "RatingEntity",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserRefId = table.Column<long>(type: "bigint", nullable: false),
                    ChatId = table.Column<long>(type: "bigint", nullable: false),
                    LastGiven = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RatingEntity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RatingEntity_Users_UserRefId",
                        column: x => x.UserRefId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RatingEntity_UserRefId_ChatId",
                table: "RatingEntity",
                columns: new[] { "UserRefId", "ChatId" },
                unique: true);
        }
    }
}
