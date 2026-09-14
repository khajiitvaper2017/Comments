using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReadPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentId_IsDeleted_CreatedAtUtc",
                table: "Comments",
                columns: new[] { "ParentId", "IsDeleted", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentId_IsDeleted_Email",
                table: "Comments",
                columns: new[] { "ParentId", "IsDeleted", "Email" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentId_IsDeleted_UserName",
                table: "Comments",
                columns: new[] { "ParentId", "IsDeleted", "UserName" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_RootId_IsDeleted_CreatedAtUtc",
                table: "Comments",
                columns: new[] { "RootId", "IsDeleted", "CreatedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentId_IsDeleted_CreatedAtUtc",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentId_IsDeleted_Email",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentId_IsDeleted_UserName",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_RootId_IsDeleted_CreatedAtUtc",
                table: "Comments");
        }
    }
}
