CREATE DATABASE IF NOT EXISTS `Comments`
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_unicode_ci;

USE `Comments`;

CREATE TABLE IF NOT EXISTS `Comments` (
    `Id` char(36) NOT NULL,
    `ParentId` char(36) NULL,
    `RootId` char(36) NOT NULL,
    `UserName` varchar(100) NOT NULL,
    `Email` varchar(254) NOT NULL,
    `HomePage` varchar(2048) NULL,
    `RawText` longtext NOT NULL,
    `SanitizedText` longtext NOT NULL,
    `CreatedAtUtc` datetime(6) NOT NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `IpAddress` varchar(64) NULL,
    `UserAgent` varchar(512) NULL,
    CONSTRAINT `PK_Comments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Comments_Comments_ParentId`
        FOREIGN KEY (`ParentId`) REFERENCES `Comments` (`Id`) ON DELETE RESTRICT
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
    `CreatedAtUtc` datetime(6) NOT NULL,
    CONSTRAINT `PK_Attachments` PRIMARY KEY (`Id`),
    CONSTRAINT `FK_Attachments_Comments_CommentId`
        FOREIGN KEY (`CommentId`) REFERENCES `Comments` (`Id`) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE INDEX `IX_Attachments_CommentId` ON `Attachments` (`CommentId`);
CREATE INDEX `IX_Comments_Email` ON `Comments` (`Email`);
CREATE INDEX `IX_Comments_ParentId` ON `Comments` (`ParentId`);
CREATE INDEX `IX_Comments_RootId_CreatedAtUtc` ON `Comments` (`RootId`, `CreatedAtUtc`);
CREATE INDEX `IX_Comments_UserName` ON `Comments` (`UserName`);
