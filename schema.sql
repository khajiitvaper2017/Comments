CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
);

START TRANSACTION;
CREATE TABLE `Comments` (
    `Id` uniqueidentifier NOT NULL,
    `ParentId` uniqueidentifier NULL,
    `RootId` uniqueidentifier NOT NULL,
    `UserName` nvarchar(100) NOT NULL,
    `Email` nvarchar(254) NOT NULL,
    `HomePage` nvarchar(2048) NULL,
    `RawText` nvarchar(max) NOT NULL,
    `SanitizedText` nvarchar(max) NOT NULL,
    `CreatedAtUtc` datetime2 NOT NULL,
    `IsDeleted` bit NOT NULL,
    `IpAddress` nvarchar(64) NULL,
    `UserAgent` nvarchar(512) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Comments_Comments_ParentId` FOREIGN KEY (`ParentId`) REFERENCES `Comments` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `Attachments` (
    `Id` uniqueidentifier NOT NULL,
    `CommentId` uniqueidentifier NOT NULL,
    `OriginalName` nvarchar(255) NOT NULL,
    `StoredName` nvarchar(255) NOT NULL,
    `ContentType` nvarchar(100) NOT NULL,
    `Size` bigint NOT NULL,
    `StorageReference` nvarchar(500) NOT NULL,
    `Width` int NULL,
    `Height` int NULL,
    `CreatedAtUtc` datetime2 NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Attachments_Comments_CommentId` FOREIGN KEY (`CommentId`) REFERENCES `Comments` (`Id`) ON DELETE CASCADE
);

CREATE INDEX `IX_Attachments_CommentId` ON `Attachments` (`CommentId`);

CREATE INDEX `IX_Comments_Email` ON `Comments` (`Email`);

CREATE INDEX `IX_Comments_ParentId` ON `Comments` (`ParentId`);

CREATE INDEX `IX_Comments_RootId_CreatedAtUtc` ON `Comments` (`RootId`, `CreatedAtUtc`);

CREATE INDEX `IX_Comments_UserName` ON `Comments` (`UserName`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260908125454_InitialCreate', '10.0.12');

ALTER TABLE `Attachments` ADD `ProcessingStatus` nvarchar(32) NOT NULL DEFAULT 'Pending';

CREATE TABLE `OutboxMessages` (
    `Id` uniqueidentifier NOT NULL,
    `Type` nvarchar(200) NOT NULL,
    `Payload` nvarchar(max) NOT NULL,
    `OccurredAtUtc` datetime2 NOT NULL,
    `AttemptCount` int NOT NULL,
    `ProcessedAtUtc` datetime2 NULL,
    `LastError` nvarchar(2000) NULL,
    PRIMARY KEY (`Id`)
);

CREATE INDEX `IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc` ON `OutboxMessages` (`ProcessedAtUtc`, `OccurredAtUtc`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260910124105_Stage7OutboxAndAttachmentProcessing', '10.0.12');

DROP INDEX IX_OutboxMessages_ProcessedAtUtc_OccurredAtUtc ON OutboxMessages;

ALTER TABLE `OutboxMessages` ADD `DeadLetteredAtUtc` datetime2 NULL;

CREATE INDEX `IX_OutboxMessages_ProcessedAtUtc_DeadLetteredAtUtc_OccurredAtUtc` ON `OutboxMessages` (`ProcessedAtUtc`, `DeadLetteredAtUtc`, `OccurredAtUtc`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260910141619_AddOutboxDeadLetterState', '10.0.12');

CREATE INDEX `IX_Comments_ParentId_IsDeleted_CreatedAtUtc` ON `Comments` (`ParentId`, `IsDeleted`, `CreatedAtUtc`);

CREATE INDEX `IX_Comments_ParentId_IsDeleted_Email` ON `Comments` (`ParentId`, `IsDeleted`, `Email`);

CREATE INDEX `IX_Comments_ParentId_IsDeleted_UserName` ON `Comments` (`ParentId`, `IsDeleted`, `UserName`);

CREATE INDEX `IX_Comments_RootId_IsDeleted_CreatedAtUtc` ON `Comments` (`RootId`, `IsDeleted`, `CreatedAtUtc`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260914130933_AddReadPerformanceIndexes', '10.0.12');

ALTER TABLE `Comments` ADD `DescendantCount` int NOT NULL DEFAULT 0;

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

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260914131926_AddWriteTimeReplyCounts', '10.0.12');

CREATE TABLE `CommentStatistics` (
    `Id` int NOT NULL,
    `TotalReplyCount` int NOT NULL,
    PRIMARY KEY (`Id`)
);

INSERT INTO `CommentStatistics` (`Id`, `TotalReplyCount`)
VALUES (1, 0);
SELECT ROW_COUNT();


UPDATE CommentStatistics
SET TotalReplyCount = (SELECT COUNT(*) FROM Comments WHERE ParentId IS NOT NULL AND IsDeleted = 0)
WHERE Id = 1;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260914132232_AddCommentStatistics', '10.0.12');

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Comments_Email'
      AND object_id = OBJECT_ID(N'[dbo].[Comments]'))
    DROP INDEX [IX_Comments_Email] ON [dbo].[Comments];

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Comments_ParentId'
      AND object_id = OBJECT_ID(N'[dbo].[Comments]'))
    DROP INDEX [IX_Comments_ParentId] ON [dbo].[Comments];

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Comments_ParentId_IsDeleted_CreatedAtUtc_Id'
      AND object_id = OBJECT_ID(N'[dbo].[Comments]'))
    DROP INDEX [IX_Comments_ParentId_IsDeleted_CreatedAtUtc_Id] ON [dbo].[Comments];

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Comments_ParentId_IsDeleted_Email_Id'
      AND object_id = OBJECT_ID(N'[dbo].[Comments]'))
    DROP INDEX [IX_Comments_ParentId_IsDeleted_Email_Id] ON [dbo].[Comments];

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Comments_ParentId_IsDeleted_UserName_Id'
      AND object_id = OBJECT_ID(N'[dbo].[Comments]'))
    DROP INDEX [IX_Comments_ParentId_IsDeleted_UserName_Id] ON [dbo].[Comments];

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Comments_RootId_CreatedAtUtc'
      AND object_id = OBJECT_ID(N'[dbo].[Comments]'))
    DROP INDEX [IX_Comments_RootId_CreatedAtUtc] ON [dbo].[Comments];

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Comments_RootId_IsDeleted_CreatedAtUtc_Id'
      AND object_id = OBJECT_ID(N'[dbo].[Comments]'))
    DROP INDEX [IX_Comments_RootId_IsDeleted_CreatedAtUtc_Id] ON [dbo].[Comments];

IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Comments_UserName'
      AND object_id = OBJECT_ID(N'[dbo].[Comments]'))
    DROP INDEX [IX_Comments_UserName] ON [dbo].[Comments];

CREATE INDEX `IX_Comments_ActiveReplies_ParentId_CreatedAtUtc_Id` ON `Comments` (`ParentId`, `CreatedAtUtc`, `Id`);

CREATE INDEX `IX_Comments_ActiveRoots_CreatedAtUtc_Id` ON `Comments` (`CreatedAtUtc`, `Id`);

CREATE INDEX `IX_Comments_ActiveRoots_Email_Id` ON `Comments` (`Email`, `Id`);

CREATE INDEX `IX_Comments_ActiveRoots_UserName_Id` ON `Comments` (`UserName`, `Id`);

CREATE INDEX `IX_Comments_ActiveTree_RootId_CreatedAtUtc_Id` ON `Comments` (`RootId`, `CreatedAtUtc`, `Id`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260914214253_OptimizeCommentReadIndexes', '10.0.12');

ALTER TABLE `CommentStatistics` ADD `TotalRootCount` int NOT NULL DEFAULT 0;

UPDATE `CommentStatistics` SET `TotalRootCount` = 0
WHERE `Id` = 1;
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260915165412_AddTotalRootCount', '10.0.12');

DROP TABLE `CommentStatistics`;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260915181528_RemoveCommentStatistics', '10.0.12');

ALTER TABLE `Comments` DROP COLUMN `RawText`;

ALTER TABLE `Comments` CHANGE `SanitizedText` `Text` nvarchar(5000) NOT NULL DEFAULT '';

ALTER TABLE `Comments` CHANGE `DescendantCount` `ReplyCount` int NOT NULL DEFAULT 0;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260916100637_RenameCommentTextAndReplyCount', '10.0.12');

ALTER TABLE `OutboxMessages` MODIFY `Type` varchar(200) NOT NULL;

ALTER TABLE `OutboxMessages` MODIFY `ProcessedAtUtc` datetime(6) NULL;

ALTER TABLE `OutboxMessages` MODIFY `Payload` longtext NOT NULL;

ALTER TABLE `OutboxMessages` MODIFY `OccurredAtUtc` datetime(6) NOT NULL;

ALTER TABLE `OutboxMessages` MODIFY `LastError` varchar(2000) NULL;

ALTER TABLE `OutboxMessages` MODIFY `DeadLetteredAtUtc` datetime(6) NULL;

ALTER TABLE `OutboxMessages` MODIFY `Id` char(36) NOT NULL;

ALTER TABLE `Comments` MODIFY `UserName` varchar(100) NOT NULL;

ALTER TABLE `Comments` MODIFY `UserAgent` varchar(512) NULL;

ALTER TABLE `Comments` MODIFY `Text` varchar(5000) NOT NULL;

ALTER TABLE `Comments` MODIFY `RootId` char(36) NOT NULL;

ALTER TABLE `Comments` MODIFY `ParentId` char(36) NULL;

ALTER TABLE `Comments` MODIFY `IsDeleted` tinyint(1) NOT NULL;

ALTER TABLE `Comments` MODIFY `IpAddress` varchar(64) NULL;

ALTER TABLE `Comments` MODIFY `HomePage` varchar(2048) NULL;

ALTER TABLE `Comments` MODIFY `Email` varchar(254) NOT NULL;

ALTER TABLE `Comments` MODIFY `CreatedAtUtc` datetime(6) NOT NULL;

ALTER TABLE `Comments` MODIFY `Id` char(36) NOT NULL;

ALTER TABLE `Attachments` MODIFY `StoredName` varchar(255) NOT NULL;

ALTER TABLE `Attachments` MODIFY `StorageReference` varchar(500) NOT NULL;

ALTER TABLE `Attachments` MODIFY `ProcessingStatus` varchar(32) NOT NULL;

ALTER TABLE `Attachments` MODIFY `OriginalName` varchar(255) NOT NULL;

ALTER TABLE `Attachments` MODIFY `CreatedAtUtc` datetime(6) NOT NULL;

ALTER TABLE `Attachments` MODIFY `ContentType` varchar(100) NOT NULL;

ALTER TABLE `Attachments` MODIFY `CommentId` char(36) NOT NULL;

ALTER TABLE `Attachments` MODIFY `Id` char(36) NOT NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20260917140152_InitialMySql', '10.0.12');

COMMIT;

