using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Comments.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeCommentReadIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comments_Email",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentId_IsDeleted_CreatedAtUtc_Id",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentId_IsDeleted_Email_Id",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ParentId_IsDeleted_UserName_Id",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_RootId_CreatedAtUtc",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_RootId_IsDeleted_CreatedAtUtc_Id",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_UserName",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ActiveReplies_ParentId_CreatedAtUtc_Id",
                table: "Comments",
                columns: new[] { "ParentId", "CreatedAtUtc", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ActiveRoots_CreatedAtUtc_Id",
                table: "Comments",
                columns: new[] { "CreatedAtUtc", "Id" },
                filter: "[ParentId] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ActiveRoots_Email_Id",
                table: "Comments",
                columns: new[] { "Email", "Id" },
                filter: "[ParentId] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ActiveRoots_UserName_Id",
                table: "Comments",
                columns: new[] { "UserName", "Id" },
                filter: "[ParentId] IS NULL AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ActiveTree_RootId_CreatedAtUtc_Id",
                table: "Comments",
                columns: new[] { "RootId", "CreatedAtUtc", "Id" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Comments_ActiveReplies_ParentId_CreatedAtUtc_Id",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ActiveRoots_CreatedAtUtc_Id",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ActiveRoots_Email_Id",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ActiveRoots_UserName_Id",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ActiveTree_RootId_CreatedAtUtc_Id",
                table: "Comments");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_Email",
                table: "Comments",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentId",
                table: "Comments",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentId_IsDeleted_CreatedAtUtc_Id",
                table: "Comments",
                columns: new[] { "ParentId", "IsDeleted", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentId_IsDeleted_Email_Id",
                table: "Comments",
                columns: new[] { "ParentId", "IsDeleted", "Email", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ParentId_IsDeleted_UserName_Id",
                table: "Comments",
                columns: new[] { "ParentId", "IsDeleted", "UserName", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_RootId_CreatedAtUtc",
                table: "Comments",
                columns: new[] { "RootId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_RootId_IsDeleted_CreatedAtUtc_Id",
                table: "Comments",
                columns: new[] { "RootId", "IsDeleted", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Comments_UserName",
                table: "Comments",
                column: "UserName");
        }
    }
}
