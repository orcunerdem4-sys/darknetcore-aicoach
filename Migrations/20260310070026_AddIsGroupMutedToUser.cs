using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarkNetCore.Migrations
{
    /// <inheritdoc />
    public partial class AddIsGroupMutedToUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsGroupMuted",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "MutedUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "text", nullable: false),
                    MuterUserId = table.Column<string>(type: "text", nullable: false),
                    MutedUserId = table.Column<string>(type: "text", nullable: false),
                    GroupId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MutedUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MutedUsers_StudyGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "StudyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MutedUsers_Users_MutedUserId",
                        column: x => x.MutedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MutedUsers_Users_MuterUserId",
                        column: x => x.MuterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MutedUsers_GroupId",
                table: "MutedUsers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_MutedUsers_MutedUserId",
                table: "MutedUsers",
                column: "MutedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MutedUsers_MuterUserId",
                table: "MutedUsers",
                column: "MuterUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MutedUsers");

            migrationBuilder.DropColumn(
                name: "IsGroupMuted",
                table: "Users");
        }
    }
}
