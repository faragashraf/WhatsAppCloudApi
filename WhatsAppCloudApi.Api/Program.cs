using Serilog;
using System.Text;
// using System;
// using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using WhatsAppCloudApi.Api.BackgroundWorkers;
using WhatsAppCloudApi.Api.Extensions;
using WhatsAppCloudApi.Api.Middleware;
using WhatsAppCloudApi.Application;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.Entities;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Register IHttpContextAccessor so infrastructure handlers can access incoming request headers
builder.Services.AddHttpContextAccessor();

builder.Host.UseSerilog((context, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices();
builder.Services.AddHealthChecks();
builder.Services.AddHostedService<MessageQueueWorker>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
builder.Services.AddAuthorization();

// CORS - allow local testing and Swagger access. In production, tighten this policy.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllDev", policy =>
    {
        policy.AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader();
    });
});

var app = builder.Build();

// Ensure database schema is up-to-date and seed BASIC plan
try
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();

    // Create any tables that were added to the DbContext after the initial EnsureCreatedAsync.
    // EnsureCreatedAsync only creates the DB if it doesn't exist — it won't add new tables.
    // Use IF NOT EXISTS to safely create each missing table independently.
    var tableSqls = new Dictionary<string, string>
    {
        ["Contacts"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Contacts')
            BEGIN
                CREATE TABLE [Contacts] (
                    [ContactId] BIGINT IDENTITY(1,1) NOT NULL,
                    [CompanyId] INT NOT NULL,
                    [Name] NVARCHAR(200) NOT NULL,
                    [PhoneNumber] NVARCHAR(30) NOT NULL,
                    [Email] NVARCHAR(200) NULL,
                    [Tags] NVARCHAR(MAX) NULL,
                    [CustomFields] NVARCHAR(MAX) NULL,
                    [Source] NVARCHAR(100) NULL,
                    [Notes] NVARCHAR(MAX) NULL,
                    [IsActive] BIT NOT NULL DEFAULT 1,
                    [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAtUtc] DATETIME2 NULL,
                    CONSTRAINT [PK_Contacts] PRIMARY KEY ([ContactId]),
                    CONSTRAINT [FK_Contacts_Companies] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_Contacts_CompanyId_PhoneNumber] ON [Contacts]([CompanyId], [PhoneNumber]);
            END",
        ["Conversations"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Conversations')
            BEGIN
                CREATE TABLE [Conversations] (
                    [ConversationId] BIGINT IDENTITY(1,1) NOT NULL,
                    [CompanyId] INT NOT NULL,
                    [WhatsAppPhoneNumberId] INT NULL,
                    [ContactId] BIGINT NULL,
                    [AssignedUserId] INT NULL,
                    [ContactNumber] NVARCHAR(30) NOT NULL,
                    [ContactName] NVARCHAR(200) NULL,
                    [LastMessageContent] NVARCHAR(1000) NULL,
                    [LastMessageType] NVARCHAR(50) NULL,
                    [LastMessageAtUtc] DATETIME2 NULL,
                    [UnreadCount] INT NOT NULL DEFAULT 0,
                    [Status] NVARCHAR(50) NOT NULL DEFAULT N'OPEN',
                    [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAtUtc] DATETIME2 NULL,
                    CONSTRAINT [PK_Conversations] PRIMARY KEY ([ConversationId]),
                    CONSTRAINT [FK_Conversations_Companies] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE
                );
            END",
        ["ConversationMessages"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='ConversationMessages')
            BEGIN
                CREATE TABLE [ConversationMessages] (
                    [ConversationMessageId] BIGINT IDENTITY(1,1) NOT NULL,
                    [ConversationId] BIGINT NOT NULL,
                    [CompanyId] INT NOT NULL,
                    [Direction] NVARCHAR(20) NOT NULL,
                    [MetaMessageId] NVARCHAR(120) NULL,
                    [MessageType] NVARCHAR(50) NOT NULL,
                    [Content] NVARCHAR(MAX) NOT NULL,
                    [MediaUrl] NVARCHAR(2000) NULL,
                    [MediaMimeType] NVARCHAR(100) NULL,
                    [Status] NVARCHAR(50) NOT NULL DEFAULT N'sent',
                    [FailureReason] NVARCHAR(MAX) NULL,
                    [TimestampUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [PK_ConversationMessages] PRIMARY KEY ([ConversationMessageId]),
                    CONSTRAINT [FK_ConvMsg_Conversations] FOREIGN KEY ([ConversationId]) REFERENCES [Conversations]([ConversationId]) ON DELETE CASCADE,
                    CONSTRAINT [FK_ConvMsg_Companies] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE NO ACTION
                );
            END",
        ["Campaigns"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Campaigns')
            BEGIN
                CREATE TABLE [Campaigns] (
                    [CampaignId] BIGINT IDENTITY(1,1) NOT NULL,
                    [CompanyId] INT NOT NULL,
                    [WhatsAppPhoneNumberId] INT NULL,
                    [Name] NVARCHAR(200) NOT NULL,
                    [Description] NVARCHAR(1000) NULL,
                    [TemplateName] NVARCHAR(200) NOT NULL,
                    [LanguageCode] NVARCHAR(10) NOT NULL DEFAULT N'ar',
                    [TemplateParametersJson] NVARCHAR(MAX) NULL,
                    [Status] NVARCHAR(50) NOT NULL DEFAULT N'DRAFT',
                    [TotalContacts] INT NOT NULL DEFAULT 0,
                    [SentCount] INT NOT NULL DEFAULT 0,
                    [DeliveredCount] INT NOT NULL DEFAULT 0,
                    [ReadCount] INT NOT NULL DEFAULT 0,
                    [FailedCount] INT NOT NULL DEFAULT 0,
                    [ScheduledAtUtc] DATETIME2 NULL,
                    [StartedAtUtc] DATETIME2 NULL,
                    [CompletedAtUtc] DATETIME2 NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAtUtc] DATETIME2 NULL,
                    CONSTRAINT [PK_Campaigns] PRIMARY KEY ([CampaignId]),
                    CONSTRAINT [FK_Campaigns_Companies] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE
                );
            END",
        ["CampaignContacts"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='CampaignContacts')
            BEGIN
                CREATE TABLE [CampaignContacts] (
                    [CampaignContactId] BIGINT IDENTITY(1,1) NOT NULL,
                    [CampaignId] BIGINT NOT NULL,
                    [ContactId] BIGINT NULL,
                    [PhoneNumber] NVARCHAR(30) NOT NULL,
                    [Status] NVARCHAR(50) NOT NULL DEFAULT N'PENDING',
                    [ExternalMessageId] NVARCHAR(120) NULL,
                    [FailureReason] NVARCHAR(MAX) NULL,
                    [SentAtUtc] DATETIME2 NULL,
                    [DeliveredAtUtc] DATETIME2 NULL,
                    [ReadAtUtc] DATETIME2 NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [PK_CampaignContacts] PRIMARY KEY ([CampaignContactId]),
                    CONSTRAINT [FK_CampaignContacts_Campaigns] FOREIGN KEY ([CampaignId]) REFERENCES [Campaigns]([CampaignId]) ON DELETE CASCADE,
                    CONSTRAINT [FK_CampaignContacts_Contacts] FOREIGN KEY ([ContactId]) REFERENCES [Contacts]([ContactId]) ON DELETE SET NULL
                );
            END",
        ["AutomationRules"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='AutomationRules')
            BEGIN
                CREATE TABLE [AutomationRules] (
                    [AutomationRuleId] BIGINT IDENTITY(1,1) NOT NULL,
                    [CompanyId] INT NOT NULL,
                    [Name] NVARCHAR(200) NOT NULL,
                    [Description] NVARCHAR(1000) NULL,
                    [TriggerType] NVARCHAR(50) NOT NULL,
                    [TriggerValue] NVARCHAR(500) NOT NULL,
                    [ResponseType] NVARCHAR(50) NOT NULL,
                    [ResponseValue] NVARCHAR(MAX) NOT NULL,
                    [TemplateName] NVARCHAR(200) NULL,
                    [LanguageCode] NVARCHAR(10) NULL,
                    [Priority] INT NOT NULL DEFAULT 0,
                    [IsActive] BIT NOT NULL DEFAULT 1,
                    [HitCount] BIGINT NOT NULL DEFAULT 0,
                    [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAtUtc] DATETIME2 NULL,
                    CONSTRAINT [PK_AutomationRules] PRIMARY KEY ([AutomationRuleId]),
                    CONSTRAINT [FK_AutomationRules_Companies] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE
                );
            END",
        ["Notifications"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Notifications')
            BEGIN
                CREATE TABLE [Notifications] (
                    [NotificationId] BIGINT IDENTITY(1,1) NOT NULL,
                    [CompanyId] INT NOT NULL,
                    [CompanyUserId] INT NULL,
                    [Type] NVARCHAR(50) NOT NULL DEFAULT N'info',
                    [Title] NVARCHAR(300) NOT NULL,
                    [Body] NVARCHAR(MAX) NOT NULL,
                    [Category] NVARCHAR(100) NULL,
                    [MetadataJson] NVARCHAR(MAX) NULL,
                    [IsRead] BIT NOT NULL DEFAULT 0,
                    [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [ReadAtUtc] DATETIME2 NULL,
                    CONSTRAINT [PK_Notifications] PRIMARY KEY ([NotificationId]),
                    CONSTRAINT [FK_Notifications_Companies] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE,
                    CONSTRAINT [FK_Notifications_CompanyUsers] FOREIGN KEY ([CompanyUserId]) REFERENCES [CompanyUsers]([CompanyUserId]) ON DELETE NO ACTION
                );
            END"
    };

    // Add new columns to existing tables (safe ALTER statements)
    var alterSqls = new Dictionary<string, string>
    {
        ["WhatsAppPhoneNumbers_NewCols"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WhatsAppPhoneNumbers') AND name = 'CodeVerificationStatus')
            BEGIN
                ALTER TABLE [WhatsAppPhoneNumbers] ADD
                    [CodeVerificationStatus] NVARCHAR(50) NULL,
                    [QualityRating] NVARCHAR(50) NULL,
                    [PlatformType] NVARCHAR(50) NULL,
                    [ThroughputLevel] NVARCHAR(50) NULL,
                    [LastOnboardedTime] NVARCHAR(100) NULL,
                    [LastSyncUtc] DATETIME2 NULL;
            END",
        ["Conversations_LastInbound"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Conversations') AND name = 'LastInboundMessageAtUtc')
            BEGIN
                ALTER TABLE [Conversations] ADD [LastInboundMessageAtUtc] DATETIME2 NULL;
            END",
        ["CompanyUsers_Permissions"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CompanyUsers') AND name = 'PermissionsJson')
            BEGIN
                ALTER TABLE [CompanyUsers] ADD [PermissionsJson] NVARCHAR(MAX) NULL;
            END",
        ["CompanyUsers_SuperAdmin"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CompanyUsers') AND name = 'IsSuperAdmin')
            BEGIN
                ALTER TABLE [CompanyUsers] ADD [IsSuperAdmin] BIT NOT NULL DEFAULT 0;
            END",
        ["CompanyUsers_OTP"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CompanyUsers') AND name = 'PasswordResetOtp')
            BEGIN
                ALTER TABLE [CompanyUsers] ADD
                    [PasswordResetOtp] NVARCHAR(500) NULL,
                    [PasswordResetOtpExpiryUtc] DATETIME2 NULL;
            END",
        ["Companies_SoftDelete"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Companies') AND name = 'IsDeleted')
            BEGIN
                ALTER TABLE [Companies] ADD
                    [IsDeleted] BIT NOT NULL DEFAULT 0,
                    [SuspendedAtUtc] DATETIME2 NULL,
                    [DeletedAtUtc] DATETIME2 NULL;
            END",
    };

    var strategy = dbContext.Database.CreateExecutionStrategy();
    foreach (var (tableName, sql) in tableSqls)
    {
        try
        {
            await strategy.ExecuteAsync(async () => await dbContext.Database.ExecuteSqlRawAsync(sql));
            Log.Information("Ensured table {Table} exists.", tableName);
        }
        catch (Exception tableEx)
        {
            Log.Warning(tableEx, "Failed to create table {Table} — it may already exist or have a dependency issue.", tableName);
        }
    }
    Log.Information("Database schema verification complete.");

    // Apply ALTER statements for new columns
    foreach (var (alterName, alterSql) in alterSqls)
    {
        try
        {
            await strategy.ExecuteAsync(async () => await dbContext.Database.ExecuteSqlRawAsync(alterSql));
            Log.Information("Applied schema alteration: {AlterName}.", alterName);
        }
        catch (Exception alterEx)
        {
            Log.Warning(alterEx, "Failed to apply alteration {AlterName} — columns may already exist.", alterName);
        }
    }

    if (!await dbContext.SubscriptionPlans.AnyAsync(x => x.Code == "BASIC"))
    {
        dbContext.SubscriptionPlans.Add(new SubscriptionPlan
        {
            Name = "Basic",
            Code = "BASIC",
            TrialDays = 14,
            MaxMessagesPerMonth = 1000,
            MaxWhatsAppAccounts = 1,
            MonthlyPrice = 0m,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();
    }
}
catch (Exception ex)
{
    Log.Warning(ex, "Database initialization failed — the server will start but some features may be unavailable until the DB is reachable.");
}

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<ApiLoggingMiddleware>();
app.UseSerilogRequestLogging();

app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment() || app.Environment.IsProduction())
{
    // If the application is hosted under a virtual directory (e.g. /WhatsAppApi),
    // ensure the request PathBase is set so middleware and Swagger use the correct base path.
    const string pathBase = "/";
    app.UsePathBase(pathBase);
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        // Register multiple possible JSON URLs so UI can work whether the reverse-proxy
        // preserves the /WhatsAppApi prefix or not. The relative URL should work when
        // the UI is served under the PathBase; absolute variants are fallback choices.
        options.SwaggerEndpoint("./v1/swagger.json", "WhatsApp Cloud API (relative)");
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "WhatsApp Cloud API (root)");
        options.SwaggerEndpoint(pathBase + "/swagger/v1/swagger.json", "WhatsApp Cloud API (with PathBase)");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseCors("AllowAllDev");
app.UseRateLimiter();
app.MapHealthChecks("/health");
app.MapControllers().RequireRateLimiting("fixed");

try
{
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
