#nullable disable

using Microsoft.EntityFrameworkCore.Migrations;

namespace Comments.Infrastructure.Migrations.MySql;

/// <inheritdoc />
public partial class InitialMySql : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            "Type",
            "OutboxMessages",
            "varchar(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(200)",
            oldMaxLength: 200);

        migrationBuilder.AlterColumn<DateTime>(
            "ProcessedAtUtc",
            "OutboxMessages",
            "datetime(6)",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime2(6)",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "Payload",
            "OutboxMessages",
            "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(max)");

        migrationBuilder.AlterColumn<DateTime>(
            "OccurredAtUtc",
            "OutboxMessages",
            "datetime(6)",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2(6)");

        migrationBuilder.AlterColumn<string>(
            "LastError",
            "OutboxMessages",
            "varchar(2000)",
            maxLength: 2000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(2000)",
            oldMaxLength: 2000,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            "DeadLetteredAtUtc",
            "OutboxMessages",
            "datetime(6)",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime2(6)",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            "Id",
            "OutboxMessages",
            "char(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier(36)");

        migrationBuilder.AlterColumn<string>(
            "UserName",
            "Comments",
            "varchar(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100);

        migrationBuilder.AlterColumn<string>(
            "UserAgent",
            "Comments",
            "varchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(512)",
            oldMaxLength: 512,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "Text",
            "Comments",
            "varchar(5000)",
            maxLength: 5000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(5000)",
            oldMaxLength: 5000);

        migrationBuilder.AlterColumn<Guid>(
            "RootId",
            "Comments",
            "char(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier(36)");

        migrationBuilder.AlterColumn<Guid>(
            "ParentId",
            "Comments",
            "char(36)",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier(36)",
            oldNullable: true);

        migrationBuilder.AlterColumn<bool>(
            "IsDeleted",
            "Comments",
            "tinyint(1)",
            nullable: false,
            oldClrType: typeof(ulong),
            oldType: "bit");

        migrationBuilder.AlterColumn<string>(
            "IpAddress",
            "Comments",
            "varchar(64)",
            maxLength: 64,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(64)",
            oldMaxLength: 64,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "HomePage",
            "Comments",
            "varchar(2048)",
            maxLength: 2048,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "nvarchar(2048)",
            oldMaxLength: 2048,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "Email",
            "Comments",
            "varchar(254)",
            maxLength: 254,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(254)",
            oldMaxLength: 254);

        migrationBuilder.AlterColumn<DateTime>(
            "CreatedAtUtc",
            "Comments",
            "datetime(6)",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2(6)");

        migrationBuilder.AlterColumn<Guid>(
            "Id",
            "Comments",
            "char(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier(36)");

        migrationBuilder.AlterColumn<string>(
            "StoredName",
            "Attachments",
            "varchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(255)",
            oldMaxLength: 255);

        migrationBuilder.AlterColumn<string>(
            "StorageReference",
            "Attachments",
            "varchar(500)",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(500)",
            oldMaxLength: 500);

        migrationBuilder.AlterColumn<string>(
            "ProcessingStatus",
            "Attachments",
            "varchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(32)",
            oldMaxLength: 32);

        migrationBuilder.AlterColumn<string>(
            "OriginalName",
            "Attachments",
            "varchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(255)",
            oldMaxLength: 255);

        migrationBuilder.AlterColumn<DateTime>(
            "CreatedAtUtc",
            "Attachments",
            "datetime(6)",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime2(6)");

        migrationBuilder.AlterColumn<string>(
            "ContentType",
            "Attachments",
            "varchar(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "nvarchar(100)",
            oldMaxLength: 100);

        migrationBuilder.AlterColumn<Guid>(
            "CommentId",
            "Attachments",
            "char(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier(36)");

        migrationBuilder.AlterColumn<Guid>(
            "Id",
            "Attachments",
            "char(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "uniqueidentifier(36)");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            "Type",
            "OutboxMessages",
            "nvarchar(200)",
            maxLength: 200,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(200)",
            oldMaxLength: 200);

        migrationBuilder.AlterColumn<DateTime>(
            "ProcessedAtUtc",
            "OutboxMessages",
            "datetime2(6)",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime(6)",
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "Payload",
            "OutboxMessages",
            "nvarchar(max)",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext");

        migrationBuilder.AlterColumn<DateTime>(
            "OccurredAtUtc",
            "OutboxMessages",
            "datetime2(6)",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime(6)");

        migrationBuilder.AlterColumn<string>(
            "LastError",
            "OutboxMessages",
            "nvarchar(2000)",
            maxLength: 2000,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(2000)",
            oldMaxLength: 2000,
            oldNullable: true);

        migrationBuilder.AlterColumn<DateTime>(
            "DeadLetteredAtUtc",
            "OutboxMessages",
            "datetime2(6)",
            nullable: true,
            oldClrType: typeof(DateTime),
            oldType: "datetime(6)",
            oldNullable: true);

        migrationBuilder.AlterColumn<Guid>(
            "Id",
            "OutboxMessages",
            "uniqueidentifier(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "char(36)");

        migrationBuilder.AlterColumn<string>(
            "UserName",
            "Comments",
            "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(100)",
            oldMaxLength: 100);

        migrationBuilder.AlterColumn<string>(
            "UserAgent",
            "Comments",
            "nvarchar(512)",
            maxLength: 512,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(512)",
            oldMaxLength: 512,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "Text",
            "Comments",
            "nvarchar(5000)",
            maxLength: 5000,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(5000)",
            oldMaxLength: 5000);

        migrationBuilder.AlterColumn<Guid>(
            "RootId",
            "Comments",
            "uniqueidentifier(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "char(36)");

        migrationBuilder.AlterColumn<Guid>(
            "ParentId",
            "Comments",
            "uniqueidentifier(36)",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "char(36)",
            oldNullable: true);

        migrationBuilder.AlterColumn<ulong>(
            "IsDeleted",
            "Comments",
            "bit",
            nullable: false,
            oldClrType: typeof(bool),
            oldType: "tinyint(1)");

        migrationBuilder.AlterColumn<string>(
            "IpAddress",
            "Comments",
            "nvarchar(64)",
            maxLength: 64,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(64)",
            oldMaxLength: 64,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "HomePage",
            "Comments",
            "nvarchar(2048)",
            maxLength: 2048,
            nullable: true,
            oldClrType: typeof(string),
            oldType: "varchar(2048)",
            oldMaxLength: 2048,
            oldNullable: true);

        migrationBuilder.AlterColumn<string>(
            "Email",
            "Comments",
            "nvarchar(254)",
            maxLength: 254,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(254)",
            oldMaxLength: 254);

        migrationBuilder.AlterColumn<DateTime>(
            "CreatedAtUtc",
            "Comments",
            "datetime2(6)",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime(6)");

        migrationBuilder.AlterColumn<Guid>(
            "Id",
            "Comments",
            "uniqueidentifier(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "char(36)");

        migrationBuilder.AlterColumn<string>(
            "StoredName",
            "Attachments",
            "nvarchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)",
            oldMaxLength: 255);

        migrationBuilder.AlterColumn<string>(
            "StorageReference",
            "Attachments",
            "nvarchar(500)",
            maxLength: 500,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(500)",
            oldMaxLength: 500);

        migrationBuilder.AlterColumn<string>(
            "ProcessingStatus",
            "Attachments",
            "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(32)",
            oldMaxLength: 32);

        migrationBuilder.AlterColumn<string>(
            "OriginalName",
            "Attachments",
            "nvarchar(255)",
            maxLength: 255,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(255)",
            oldMaxLength: 255);

        migrationBuilder.AlterColumn<DateTime>(
            "CreatedAtUtc",
            "Attachments",
            "datetime2(6)",
            nullable: false,
            oldClrType: typeof(DateTime),
            oldType: "datetime(6)");

        migrationBuilder.AlterColumn<string>(
            "ContentType",
            "Attachments",
            "nvarchar(100)",
            maxLength: 100,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(100)",
            oldMaxLength: 100);

        migrationBuilder.AlterColumn<Guid>(
            "CommentId",
            "Attachments",
            "uniqueidentifier(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "char(36)");

        migrationBuilder.AlterColumn<Guid>(
            "Id",
            "Attachments",
            "uniqueidentifier(36)",
            nullable: false,
            oldClrType: typeof(Guid),
            oldType: "char(36)");
    }
}
