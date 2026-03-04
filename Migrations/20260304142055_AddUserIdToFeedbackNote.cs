using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DarkNetCore.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdToFeedbackNote : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "FeedbackNotes",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UserId",
                table: "FeedbackNotes");
        }
    }
}
