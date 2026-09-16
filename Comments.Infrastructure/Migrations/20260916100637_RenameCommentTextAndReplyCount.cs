using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameCommentTextAndReplyCount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RawText",
                table: "Comments");

            migrationBuilder.RenameColumn(
                name: "SanitizedText",
                table: "Comments",
                newName: "Text");

            migrationBuilder.RenameColumn(
                name: "DescendantCount",
                table: "Comments",
                newName: "ReplyCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Text",
                table: "Comments",
                newName: "SanitizedText");

            migrationBuilder.RenameColumn(
                name: "ReplyCount",
                table: "Comments",
                newName: "DescendantCount");

            migrationBuilder.AddColumn<string>(
                name: "RawText",
                table: "Comments",
                type: "nvarchar(max)",
                maxLength: 5000,
                nullable: false,
                defaultValue: "");
        }
    }
}
