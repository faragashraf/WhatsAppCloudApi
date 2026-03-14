using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WhatsAppCloudApi.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignRecipientVerificationAndUserTwoFactor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF COL_LENGTH('CompanyUsers', 'TwoFactorEnabled') IS NULL
BEGIN
    ALTER TABLE [CompanyUsers]
        ADD [TwoFactorEnabled] bit NOT NULL
            CONSTRAINT [DF_CompanyUsers_TwoFactorEnabled] DEFAULT CAST(0 AS bit);
END;

IF COL_LENGTH('CompanyUsers', 'TwoFactorEnabledAtUtc') IS NULL
BEGIN
    ALTER TABLE [CompanyUsers] ADD [TwoFactorEnabledAtUtc] datetime2 NULL;
END;

IF COL_LENGTH('CompanyUsers', 'TwoFactorSecretProtected') IS NULL
BEGIN
    ALTER TABLE [CompanyUsers] ADD [TwoFactorSecretProtected] nvarchar(max) NULL;
END;

IF COL_LENGTH('CompanyUsers', 'TwoFactorUpdatedAtUtc') IS NULL
BEGIN
    ALTER TABLE [CompanyUsers] ADD [TwoFactorUpdatedAtUtc] datetime2 NULL;
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NULL
BEGIN
    CREATE TABLE [CampaignContacts] (
        [CampaignContactId] bigint IDENTITY(1,1) NOT NULL,
        [CampaignId] bigint NOT NULL,
        [ContactId] bigint NULL,
        [PhoneNumber] nvarchar(30) NOT NULL,
        [Status] nvarchar(50) NOT NULL
            CONSTRAINT [DF_CampaignContacts_Status] DEFAULT N'PENDING',
        [ExternalMessageId] nvarchar(120) NULL,
        [FailureReason] nvarchar(max) NULL,
        [SentAtUtc] datetime2 NULL,
        [DeliveredAtUtc] datetime2 NULL,
        [ReadAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL
            CONSTRAINT [DF_CampaignContacts_CreatedAtUtc] DEFAULT GETUTCDATE(),
        CONSTRAINT [PK_CampaignContacts] PRIMARY KEY ([CampaignContactId])
    );
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'IsWhatsAppAccountConfirmed') IS NULL
BEGIN
    ALTER TABLE [CampaignContacts]
        ADD [IsWhatsAppAccountConfirmed] bit NOT NULL
            CONSTRAINT [DF_CampaignContacts_IsWhatsAppAccountConfirmed] DEFAULT CAST(0 AS bit);
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'QueuedAtUtc') IS NULL
BEGIN
    ALTER TABLE [CampaignContacts] ADD [QueuedAtUtc] datetime2 NULL;
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'WhatsAppLookupCheckedAtUtc') IS NULL
BEGIN
    ALTER TABLE [CampaignContacts] ADD [WhatsAppLookupCheckedAtUtc] datetime2 NULL;
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'WhatsAppLookupError') IS NULL
BEGIN
    ALTER TABLE [CampaignContacts] ADD [WhatsAppLookupError] nvarchar(1000) NULL;
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'WhatsAppLookupStatus') IS NULL
BEGIN
    ALTER TABLE [CampaignContacts]
        ADD [WhatsAppLookupStatus] nvarchar(30) NOT NULL
            CONSTRAINT [DF_CampaignContacts_WhatsAppLookupStatus] DEFAULT N'UNVERIFIED';
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'WhatsAppLookupWaId') IS NULL
BEGIN
    ALTER TABLE [CampaignContacts] ADD [WhatsAppLookupWaId] nvarchar(40) NULL;
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'[CampaignContacts]')
          AND name = N'IX_CampaignContacts_CampaignId')
BEGIN
    CREATE INDEX [IX_CampaignContacts_CampaignId] ON [CampaignContacts]([CampaignId]);
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'[CampaignContacts]')
          AND name = N'IX_CampaignContacts_ContactId')
BEGIN
    CREATE INDEX [IX_CampaignContacts_ContactId] ON [CampaignContacts]([ContactId]);
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND OBJECT_ID(N'[Campaigns]', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CampaignContacts_Campaigns_CampaignId')
BEGIN
    ALTER TABLE [CampaignContacts]
        WITH CHECK ADD CONSTRAINT [FK_CampaignContacts_Campaigns_CampaignId]
        FOREIGN KEY ([CampaignId]) REFERENCES [Campaigns]([CampaignId]) ON DELETE CASCADE;
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND OBJECT_ID(N'[Contacts]', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_CampaignContacts_Contacts_ContactId')
BEGIN
    ALTER TABLE [CampaignContacts]
        WITH CHECK ADD CONSTRAINT [FK_CampaignContacts_Contacts_ContactId]
        FOREIGN KEY ([ContactId]) REFERENCES [Contacts]([ContactId]) ON DELETE NO ACTION;
END;
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
IF COL_LENGTH('CompanyUsers', 'TwoFactorEnabled') IS NOT NULL
BEGIN
    DECLARE @companyUsersTwoFactorEnabledDefault nvarchar(128);
    SELECT @companyUsersTwoFactorEnabledDefault = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t
        ON t.object_id = c.object_id
    WHERE t.name = N'CompanyUsers'
      AND c.name = N'TwoFactorEnabled';

    IF @companyUsersTwoFactorEnabledDefault IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [CompanyUsers] DROP CONSTRAINT [' + @companyUsersTwoFactorEnabledDefault + N']');
    END;

    ALTER TABLE [CompanyUsers] DROP COLUMN [TwoFactorEnabled];
END;

IF COL_LENGTH('CompanyUsers', 'TwoFactorEnabledAtUtc') IS NOT NULL
BEGIN
    ALTER TABLE [CompanyUsers] DROP COLUMN [TwoFactorEnabledAtUtc];
END;

IF COL_LENGTH('CompanyUsers', 'TwoFactorSecretProtected') IS NOT NULL
BEGIN
    ALTER TABLE [CompanyUsers] DROP COLUMN [TwoFactorSecretProtected];
END;

IF COL_LENGTH('CompanyUsers', 'TwoFactorUpdatedAtUtc') IS NOT NULL
BEGIN
    ALTER TABLE [CompanyUsers] DROP COLUMN [TwoFactorUpdatedAtUtc];
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'IsWhatsAppAccountConfirmed') IS NOT NULL
BEGIN
    DECLARE @campaignContactsConfirmedDefault nvarchar(128);
    SELECT @campaignContactsConfirmedDefault = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t
        ON t.object_id = c.object_id
    WHERE t.name = N'CampaignContacts'
      AND c.name = N'IsWhatsAppAccountConfirmed';

    IF @campaignContactsConfirmedDefault IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [CampaignContacts] DROP CONSTRAINT [' + @campaignContactsConfirmedDefault + N']');
    END;

    ALTER TABLE [CampaignContacts] DROP COLUMN [IsWhatsAppAccountConfirmed];
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'QueuedAtUtc') IS NOT NULL
BEGIN
    ALTER TABLE [CampaignContacts] DROP COLUMN [QueuedAtUtc];
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'WhatsAppLookupCheckedAtUtc') IS NOT NULL
BEGIN
    ALTER TABLE [CampaignContacts] DROP COLUMN [WhatsAppLookupCheckedAtUtc];
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'WhatsAppLookupError') IS NOT NULL
BEGIN
    ALTER TABLE [CampaignContacts] DROP COLUMN [WhatsAppLookupError];
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'WhatsAppLookupStatus') IS NOT NULL
BEGIN
    DECLARE @campaignContactsStatusDefault nvarchar(128);
    SELECT @campaignContactsStatusDefault = dc.name
    FROM sys.default_constraints dc
    INNER JOIN sys.columns c
        ON c.default_object_id = dc.object_id
    INNER JOIN sys.tables t
        ON t.object_id = c.object_id
    WHERE t.name = N'CampaignContacts'
      AND c.name = N'WhatsAppLookupStatus';

    IF @campaignContactsStatusDefault IS NOT NULL
    BEGIN
        EXEC(N'ALTER TABLE [CampaignContacts] DROP CONSTRAINT [' + @campaignContactsStatusDefault + N']');
    END;

    ALTER TABLE [CampaignContacts] DROP COLUMN [WhatsAppLookupStatus];
END;

IF OBJECT_ID(N'[CampaignContacts]', N'U') IS NOT NULL
    AND COL_LENGTH('CampaignContacts', 'WhatsAppLookupWaId') IS NOT NULL
BEGIN
    ALTER TABLE [CampaignContacts] DROP COLUMN [WhatsAppLookupWaId];
END;
""");
        }
    }
}
