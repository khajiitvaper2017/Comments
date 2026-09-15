using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCommentStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommentStatistics");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommentStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TotalReplyCount = table.Column<int>(type: "int", nullable: false),
                    TotalRootCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentStatistics", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CommentStatistics",
                columns: new[] { "Id", "TotalReplyCount", "TotalRootCount" },
                values: new object[] { 1, 0, 0 });
        }
    }
}
