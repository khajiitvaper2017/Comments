CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (
    `MigrationId` varchar(150) NOT NULL,
    `ProductVersion` varchar(32) NOT NULL,
    PRIMARY KEY (`MigrationId`)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `Comments` (
    `Id` char(36) NOT NULL,
    `ParentId` char(36) NULL,
    `RootId` char(36) NOT NULL,
    `UserName` varchar(100) NOT NULL,
    `Email` varchar(254) NOT NULL,
    `HomePage` varchar(2048) NULL,
    `Text` varchar(5000) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `ReplyCount` int NOT NULL DEFAULT 0,
    `IsDeleted` tinyint(1) NOT NULL,
    `IpAddress` varchar(64) NULL,
    `UserAgent` varchar(512) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Comments_Comments_ParentId`
        FOREIGN KEY (`ParentId`) REFERENCES `Comments` (`Id`) ON DELETE RESTRICT,
    INDEX `IX_Comments_ActiveRoots_CreatedAtUtc_Id` (`CreatedAtUtc`, `Id`),
    INDEX `IX_Comments_ActiveRoots_Email_Id` (`Email`, `Id`),
    INDEX `IX_Comments_ActiveRoots_UserName_Id` (`UserName`, `Id`),
    INDEX `IX_Comments_ActiveReplies_ParentId_CreatedAtUtc_Id` (`ParentId`, `CreatedAtUtc`, `Id`),
    INDEX `IX_Comments_ActiveTree_RootId_CreatedAtUtc_Id` (`RootId`, `CreatedAtUtc`, `Id`)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `Attachments` (
    `Id` char(36) NOT NULL,
    `CommentId` char(36) NOT NULL,
    `OriginalName` varchar(255) NOT NULL,
    `StoredName` varchar(255) NOT NULL,
    `ContentType` varchar(100) NOT NULL,
    `Size` bigint NOT NULL,
    `StorageReference` varchar(500) NOT NULL,
    `Width` int NULL,
    `Height` int NULL,
    `ProcessingStatus` varchar(32) NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Attachments_Comments_CommentId`
        FOREIGN KEY (`CommentId`) REFERENCES `Comments` (`Id`) ON DELETE CASCADE,
    INDEX `IX_Attachments_CommentId` (`CommentId`)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS `OutboxMessages` (
    `Id` char(36) NOT NULL,
    `Type` varchar(200) NOT NULL,
    `Payload` longtext NOT NULL,
    `OccurredAtUtc` datetime(6) NOT NULL,
    `AttemptCount` int NOT NULL,
    `ProcessedAtUtc` datetime(6) NULL,
    `DeadLetteredAtUtc` datetime(6) NULL,
    `LastError` varchar(2000) NULL,
    PRIMARY KEY (`Id`),
    INDEX `IX_OutboxMessages_ProcessedAtUtc_DeadLetteredAtUtc_OccurredAtUtc`
        (`ProcessedAtUtc`, `DeadLetteredAtUtc`, `OccurredAtUtc`)
) ENGINE=InnoDB;

INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES
    ('20260908125454_InitialCreate', '10.0.12'),
    ('20260910124105_Stage7OutboxAndAttachmentProcessing', '10.0.12'),
    ('20260910141619_AddOutboxDeadLetterState', '10.0.12'),
    ('20260914130933_AddReadPerformanceIndexes', '10.0.12'),
    ('20260914131926_AddWriteTimeReplyCounts', '10.0.12'),
    ('20260914132232_AddCommentStatistics', '10.0.12'),
    ('20260914214253_OptimizeCommentReadIndexes', '10.0.12'),
    ('20260915165412_AddTotalRootCount', '10.0.12'),
    ('20260915181528_RemoveCommentStatistics', '10.0.12'),
    ('20260916100637_RenameCommentTextAndReplyCount', '10.0.12'),
    ('20260917140152_InitialMySql', '10.0.12');
