IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [Comments] (
    [Id] uniqueidentifier NOT NULL,
    [ParentId] uniqueidentifier NULL,
    [RootId] uniqueidentifier NOT NULL,
    [UserName] nvarchar(100) NOT NULL,
    [Email] nvarchar(254) NOT NULL,
    [HomePage] nvarchar(2048) NULL,
    [RawText] nvarchar(max) NOT NULL,
    [SanitizedText] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [IsDeleted] bit NOT NULL,
    [IpAddress] nvarchar(64) NULL,
    [UserAgent] nvarchar(512) NULL,
    CONSTRAINT [PK_Comments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Comments_Comments_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Comments] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [Attachments] (
    [Id] uniqueidentifier NOT NULL,
    [CommentId] uniqueidentifier NOT NULL,
    [OriginalName] nvarchar(255) NOT NULL,
    [StoredName] nvarchar(255) NOT NULL,
    [ContentType] nvarchar(100) NOT NULL,
    [Size] bigint NOT NULL,
    [StorageReference] nvarchar(500) NOT NULL,
    [Width] int NULL,
    [Height] int NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Attachments_Comments_CommentId] FOREIGN KEY ([CommentId]) REFERENCES [Comments] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_Attachments_CommentId] ON [Attachments] ([CommentId]);

CREATE INDEX [IX_Comments_Email] ON [Comments] ([Email]);

CREATE INDEX [IX_Comments_ParentId] ON [Comments] ([ParentId]);

CREATE INDEX [IX_Comments_RootId_CreatedAtUtc] ON [Comments] ([RootId], [CreatedAtUtc]);

CREATE INDEX [IX_Comments_UserName] ON [Comments] ([UserName]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260908125454_InitialCreate', N'10.0.11');

COMMIT;
GO

