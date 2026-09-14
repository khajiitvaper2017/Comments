using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCommentStatistics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommentStatistics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TotalReplyCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommentStatistics", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "CommentStatistics",
                columns: new[] { "Id", "TotalReplyCount" },
                values: new object[] { 1, 0 });

            migrationBuilder.Sql("""
                UPDATE CommentStatistics
                SET TotalReplyCount = (SELECT COUNT(*) FROM Comments WHERE ParentId IS NOT NULL AND IsDeleted = 0)
                WHERE Id = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommentStatistics");
        }
    }
}
