using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260311170000_LegacySchemaReconciliation")]
public sealed class LegacySchemaReconciliation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH('Contacts', 'OwnerUserId') IS NULL
BEGIN
    ALTER TABLE [Contacts] ADD [OwnerUserId] INT NULL;
END;

IF COL_LENGTH('Contacts', 'FirstSeenAtUtc') IS NULL
BEGIN
    ALTER TABLE [Contacts] ADD [FirstSeenAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_Contacts_FirstSeenAtUtc] DEFAULT GETUTCDATE();
END;

IF COL_LENGTH('Contacts', 'LastSeenAtUtc') IS NULL
BEGIN
    ALTER TABLE [Contacts] ADD [LastSeenAtUtc] DATETIME2 NULL;
END;

IF COL_LENGTH('Contacts', 'LastInboundMessageAtUtc') IS NULL
BEGIN
    ALTER TABLE [Contacts] ADD [LastInboundMessageAtUtc] DATETIME2 NULL;
END;

IF COL_LENGTH('Contacts', 'LastOutboundMessageAtUtc') IS NULL
BEGIN
    ALTER TABLE [Contacts] ADD [LastOutboundMessageAtUtc] DATETIME2 NULL;
END;

IF COL_LENGTH('Contacts', 'OwnerAssignedAtUtc') IS NULL
BEGIN
    ALTER TABLE [Contacts] ADD [OwnerAssignedAtUtc] DATETIME2 NULL;
END;

IF COL_LENGTH('Conversations', 'LastInboundMessageAtUtc') IS NULL
BEGIN
    ALTER TABLE [Conversations] ADD [LastInboundMessageAtUtc] DATETIME2 NULL;
END;

IF COL_LENGTH('Messages', 'ContactId') IS NULL
BEGIN
    ALTER TABLE [Messages] ADD [ContactId] BIGINT NULL;
END;

IF COL_LENGTH('Messages', 'ConversationId') IS NULL
BEGIN
    ALTER TABLE [Messages] ADD [ConversationId] BIGINT NULL;
END;

IF COL_LENGTH('Messages', 'CreatedByUserId') IS NULL
BEGIN
    ALTER TABLE [Messages] ADD [CreatedByUserId] INT NULL;
END;

IF COL_LENGTH('Messages', 'Source') IS NULL
BEGIN
    ALTER TABLE [Messages] ADD [Source] NVARCHAR(30) NOT NULL CONSTRAINT [DF_Messages_Source] DEFAULT N'DIRECT';
END;

IF COL_LENGTH('ConversationMessages', 'MessageId') IS NULL
BEGIN
    ALTER TABLE [ConversationMessages] ADD [MessageId] BIGINT NULL;
END;

IF OBJECT_ID(N'[CompanyRoutingSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [CompanyRoutingSettings] (
        [CompanyRoutingSettingsId] INT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NOT NULL,
        [AssignmentMode] NVARCHAR(20) NOT NULL CONSTRAINT [DF_CompanyRoutingSettings_AssignmentMode] DEFAULT N'MANUAL',
        [AutoAssignmentStrategy] NVARCHAR(30) NOT NULL CONSTRAINT [DF_CompanyRoutingSettings_AutoAssignmentStrategy] DEFAULT N'ROUND_ROBIN',
        [RespectExistingContactOwner] BIT NOT NULL CONSTRAINT [DF_CompanyRoutingSettings_RespectExistingContactOwner] DEFAULT 1,
        [ReassignWhenOwnerInactive] BIT NOT NULL CONSTRAINT [DF_CompanyRoutingSettings_ReassignWhenOwnerInactive] DEFAULT 1,
        [ManualReassignmentUpdatesContactOwner] BIT NOT NULL CONSTRAINT [DF_CompanyRoutingSettings_ManualReassignmentUpdatesContactOwner] DEFAULT 0,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_CompanyRoutingSettings_CreatedAtUtc] DEFAULT GETUTCDATE(),
        [UpdatedAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_CompanyRoutingSettings] PRIMARY KEY ([CompanyRoutingSettingsId]),
        CONSTRAINT [FK_CompanyRoutingSettings_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE
    );
END;

IF OBJECT_ID(N'[CompanyUserRoutingSettings]', N'U') IS NULL
BEGIN
    CREATE TABLE [CompanyUserRoutingSettings] (
        [CompanyUserRoutingSettingsId] INT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NOT NULL,
        [CompanyUserId] INT NOT NULL,
        [CanReceiveManualAssignments] BIT NOT NULL CONSTRAINT [DF_CompanyUserRoutingSettings_CanReceiveManualAssignments] DEFAULT 1,
        [CanReceiveAutoAssignments] BIT NOT NULL CONSTRAINT [DF_CompanyUserRoutingSettings_CanReceiveAutoAssignments] DEFAULT 1,
        [LastAutoAssignedAtUtc] DATETIME2 NULL,
        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_CompanyUserRoutingSettings_CreatedAtUtc] DEFAULT GETUTCDATE(),
        [UpdatedAtUtc] DATETIME2 NULL,
        CONSTRAINT [PK_CompanyUserRoutingSettings] PRIMARY KEY ([CompanyUserRoutingSettingsId]),
        CONSTRAINT [FK_CompanyUserRoutingSettings_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE,
        CONSTRAINT [FK_CompanyUserRoutingSettings_CompanyUsers_CompanyUserId] FOREIGN KEY ([CompanyUserId]) REFERENCES [CompanyUsers]([CompanyUserId]) ON DELETE NO ACTION
    );
END;

IF OBJECT_ID(N'[ConversationAssignmentHistory]', N'U') IS NULL
BEGIN
    CREATE TABLE [ConversationAssignmentHistory] (
        [ConversationAssignmentHistoryId] BIGINT IDENTITY(1,1) NOT NULL,
        [CompanyId] INT NOT NULL,
        [ConversationId] BIGINT NOT NULL,
        [ContactId] BIGINT NULL,
        [PreviousAssignedUserId] INT NULL,
        [NewAssignedUserId] INT NULL,
        [PreviousOwnerUserId] INT NULL,
        [NewOwnerUserId] INT NULL,
        [AssignmentMode] NVARCHAR(20) NOT NULL CONSTRAINT [DF_ConversationAssignmentHistory_AssignmentMode] DEFAULT N'MANUAL',
        [Reason] NVARCHAR(100) NOT NULL,
        [ChangedByUserId] INT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [ChangedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_ConversationAssignmentHistory_ChangedAtUtc] DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_ConversationAssignmentHistory] PRIMARY KEY ([ConversationAssignmentHistoryId]),
        CONSTRAINT [FK_ConversationAssignmentHistory_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE,
        CONSTRAINT [FK_ConversationAssignmentHistory_Conversations_ConversationId] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations]([ConversationId]) ON DELETE NO ACTION
    );
END;
""");

        migrationBuilder.Sql("""
UPDATE [Messages]
SET [Source] = N'DIRECT'
WHERE [Source] IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyRoutingSettings_CompanyId' AND object_id = OBJECT_ID('CompanyRoutingSettings'))
BEGIN
    CREATE UNIQUE INDEX [IX_CompanyRoutingSettings_CompanyId] ON [CompanyRoutingSettings]([CompanyId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CompanyUserRoutingSettings_CompanyId_CompanyUserId' AND object_id = OBJECT_ID('CompanyUserRoutingSettings'))
BEGIN
    CREATE UNIQUE INDEX [IX_CompanyUserRoutingSettings_CompanyId_CompanyUserId] ON [CompanyUserRoutingSettings]([CompanyId], [CompanyUserId]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Contacts_CompanyId_OwnerUserId_IsActive' AND object_id = OBJECT_ID('Contacts'))
BEGIN
    CREATE INDEX [IX_Contacts_CompanyId_OwnerUserId_IsActive] ON [Contacts]([CompanyId], [OwnerUserId], [IsActive]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Contacts_CompanyId_LastSeenAtUtc' AND object_id = OBJECT_ID('Contacts'))
BEGIN
    CREATE INDEX [IX_Contacts_CompanyId_LastSeenAtUtc] ON [Contacts]([CompanyId], [LastSeenAtUtc]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Messages_CompanyId_ConversationId_CreatedAtUtc' AND object_id = OBJECT_ID('Messages'))
BEGIN
    CREATE INDEX [IX_Messages_CompanyId_ConversationId_CreatedAtUtc] ON [Messages]([CompanyId], [ConversationId], [CreatedAtUtc]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Messages_CompanyId_ContactId_CreatedAtUtc' AND object_id = OBJECT_ID('Messages'))
BEGIN
    CREATE INDEX [IX_Messages_CompanyId_ContactId_CreatedAtUtc] ON [Messages]([CompanyId], [ContactId], [CreatedAtUtc]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ConversationMessages_ConversationId_TimestampUtc' AND object_id = OBJECT_ID('ConversationMessages'))
BEGIN
    CREATE INDEX [IX_ConversationMessages_ConversationId_TimestampUtc] ON [ConversationMessages]([ConversationId], [TimestampUtc]);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ConversationMessages_MessageId' AND object_id = OBJECT_ID('ConversationMessages'))
BEGIN
    CREATE UNIQUE INDEX [IX_ConversationMessages_MessageId] ON [ConversationMessages]([MessageId]) WHERE [MessageId] IS NOT NULL;
END;
""");

        migrationBuilder.Sql("""

INSERT INTO [CompanyRoutingSettings] (
    [CompanyId],
    [AssignmentMode],
    [AutoAssignmentStrategy],
    [RespectExistingContactOwner],
    [ReassignWhenOwnerInactive],
    [ManualReassignmentUpdatesContactOwner],
    [CreatedAtUtc]
)
SELECT
    c.[CompanyId],
    N'MANUAL',
    N'ROUND_ROBIN',
    1,
    1,
    0,
    GETUTCDATE()
FROM [Companies] c
WHERE NOT EXISTS (
    SELECT 1
    FROM [CompanyRoutingSettings] rs
    WHERE rs.[CompanyId] = c.[CompanyId]
);

INSERT INTO [CompanyUserRoutingSettings] (
    [CompanyId],
    [CompanyUserId],
    [CanReceiveManualAssignments],
    [CanReceiveAutoAssignments],
    [CreatedAtUtc]
)
SELECT
    cu.[CompanyId],
    cu.[CompanyUserId],
    1,
    1,
    GETUTCDATE()
FROM [CompanyUsers] cu
WHERE NOT EXISTS (
    SELECT 1
    FROM [CompanyUserRoutingSettings] urs
    WHERE urs.[CompanyId] = cu.[CompanyId]
      AND urs.[CompanyUserId] = cu.[CompanyUserId]
);
""");

        migrationBuilder.Sql("""

;WITH MissingContacts AS (
    SELECT
        c.[CompanyId],
        c.[ContactNumber],
        MAX(NULLIF(LTRIM(RTRIM(c.[ContactName])), N'')) AS [ContactName],
        MIN(ISNULL(c.[CreatedAtUtc], GETUTCDATE())) AS [FirstSeenAtUtc],
        MAX(COALESCE(c.[LastMessageAtUtc], c.[CreatedAtUtc], GETUTCDATE())) AS [LastSeenAtUtc],
        MAX(c.[LastInboundMessageAtUtc]) AS [LastInboundMessageAtUtc]
    FROM [Conversations] c
    LEFT JOIN [Contacts] ct
        ON ct.[CompanyId] = c.[CompanyId]
       AND ct.[PhoneNumber] = c.[ContactNumber]
    WHERE c.[ContactId] IS NULL
      AND c.[ContactNumber] IS NOT NULL
      AND ct.[ContactId] IS NULL
    GROUP BY c.[CompanyId], c.[ContactNumber]
)
INSERT INTO [Contacts] (
    [CompanyId],
    [Name],
    [PhoneNumber],
    [Source],
    [IsActive],
    [FirstSeenAtUtc],
    [LastSeenAtUtc],
    [LastInboundMessageAtUtc],
    [CreatedAtUtc],
    [UpdatedAtUtc]
)
SELECT
    mc.[CompanyId],
    COALESCE(mc.[ContactName], mc.[ContactNumber]),
    mc.[ContactNumber],
    N'legacy_conversation_backfill',
    1,
    mc.[FirstSeenAtUtc],
    mc.[LastSeenAtUtc],
    mc.[LastInboundMessageAtUtc],
    mc.[FirstSeenAtUtc],
    GETUTCDATE()
FROM MissingContacts mc;

UPDATE c
SET
    c.[ContactId] = ct.[ContactId],
    c.[ContactName] = COALESCE(NULLIF(LTRIM(RTRIM(c.[ContactName])), N''), ct.[Name])
FROM [Conversations] c
INNER JOIN [Contacts] ct
    ON ct.[CompanyId] = c.[CompanyId]
   AND ct.[PhoneNumber] = c.[ContactNumber]
WHERE c.[ContactId] IS NULL
  AND c.[ContactNumber] IS NOT NULL;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
