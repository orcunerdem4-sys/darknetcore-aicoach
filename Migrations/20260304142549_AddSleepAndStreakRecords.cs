using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarkNetCore.Migrations
{
    /// <inheritdoc />
    public partial class AddSleepAndStreakRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SleepRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    SleepStart = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    SleepEnd = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    QualityScore = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SleepRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SleepRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "StreakRecords",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    LastCompletedDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CurrentStreak = table.Column<int>(type: "integer", nullable: false),
                    LongestStreak = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StreakRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StreakRecords_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SleepRecords_UserId",
                table: "SleepRecords",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_StreakRecords_UserId",
                table: "StreakRecords",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SleepRecords");

            migrationBuilder.DropTable(
                name: "StreakRecords");
        }
    }
}
