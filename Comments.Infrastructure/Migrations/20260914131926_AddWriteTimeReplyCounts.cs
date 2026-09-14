using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWriteTimeReplyCounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DescendantCount",
                table: "Comments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                ;WITH Descendants AS
                (
                    SELECT ParentId AS AncestorId, Id AS DescendantId
                    FROM Comments
                    WHERE ParentId IS NOT NULL AND IsDeleted = 0

                    UNION ALL

                    SELECT d.AncestorId, c.Id
                    FROM Descendants d
                    INNER JOIN Comments c ON c.ParentId = d.DescendantId
                    WHERE c.IsDeleted = 0
                )
                UPDATE c
                SET DescendantCount = counts.ReplyCount
                FROM Comments c
                INNER JOIN
                (
                    SELECT AncestorId, COUNT(*) AS ReplyCount
                    FROM Descendants
                    GROUP BY AncestorId
                ) counts ON counts.AncestorId = c.Id
                OPTION (MAXRECURSION 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DescendantCount",
                table: "Comments");
        }
    }
}
