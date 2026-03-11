using Serilog;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
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

builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
    options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(20);
    options.Limits.KeepAliveTimeout = TimeSpan.FromMinutes(2);
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10 MB
    options.ValueLengthLimit = 1024 * 1024;
});

builder.Services.AddRequestTimeouts(options =>
{
    options.DefaultPolicy = new Microsoft.AspNetCore.Http.Timeouts.RequestTimeoutPolicy
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    options.AddPolicy("upload-timeout", TimeSpan.FromSeconds(45));
    options.AddPolicy("webhook-timeout", TimeSpan.FromSeconds(20));
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 2;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

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
builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddHealthChecks();
builder.Services.AddHostedService<MessageQueueWorker>();
builder.Services.AddHostedService<WebhookQueueWorker>();

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var envJwtKey = Environment.GetEnvironmentVariable("JWT__KEY")
    ?? Environment.GetEnvironmentVariable("JWT_KEY");
if (!string.IsNullOrWhiteSpace(envJwtKey))
{
    jwtOptions.Key = envJwtKey;
}

if (string.IsNullOrWhiteSpace(jwtOptions.Key)
    || jwtOptions.Key.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase)
    || jwtOptions.Key.StartsWith("__SET_", StringComparison.OrdinalIgnoreCase)
    || jwtOptions.Key.Length < 64)
{
    throw new InvalidOperationException("Jwt:Key must be a non-placeholder secret with at least 64 characters (preferably from JWT__KEY env var).");
}

if (jwtOptions.AccessTokenMinutes is < 5 or > 30)
{
    throw new InvalidOperationException("Jwt:AccessTokenMinutes must be between 5 and 30.");
}

if (jwtOptions.RefreshTokenDays is < 1 or > 14)
{
    throw new InvalidOperationException("Jwt:RefreshTokenDays must be between 1 and 14.");
}

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken = false;
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = signingKey,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = "UserId",
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var tokenType = context.Principal?.FindFirst("token_type")?.Value;
                if (!string.Equals(tokenType, "access", StringComparison.OrdinalIgnoreCase))
                {
                    context.Fail("Invalid token type.");
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddProblemDetails();

var configuredCorsOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?.Where(origin => Uri.TryCreate(origin, UriKind.Absolute, out _))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultCors", policy =>
    {
        if (configuredCorsOrigins.Length > 0)
        {
            policy.WithOrigins(configuredCorsOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders(CorrelationIdMiddleware.HeaderName);
            return;
        }

        if (builder.Environment.IsDevelopment())
        {
            policy.WithOrigins(
                    "http://localhost:4200",
                    "https://localhost:4200",
                    "http://127.0.0.1:4200")
                .AllowAnyMethod()
                .AllowAnyHeader()
                .WithExposedHeaders(CorrelationIdMiddleware.HeaderName);
        }
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
                    [FileName] NVARCHAR(255) NULL,
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
            END",
        ["WebhookLogs"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='WebhookLogs')
            BEGIN
                CREATE TABLE [WebhookLogs] (
                    [WebhookLogId] BIGINT IDENTITY(1,1) NOT NULL,
                    [CompanyId] INT NOT NULL,
                    [PhoneNumberId] NVARCHAR(100) NOT NULL,
                    [Payload] NVARCHAR(MAX) NOT NULL,
                    [Summary] NVARCHAR(200) NULL,
                    [SignatureValid] BIT NULL,
                    [CorrelationId] NVARCHAR(100) NULL,
                    [CreatedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [PK_WebhookLogs] PRIMARY KEY ([WebhookLogId]),
                    CONSTRAINT [FK_WebhookLogs_Companies] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_WebhookLogs_CompanyId_CreatedAtUtc] ON [WebhookLogs]([CompanyId], [CreatedAtUtc] DESC);
            END",
        ["WebhookInbox"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='WebhookInbox')
            BEGIN
                CREATE TABLE [WebhookInbox] (
                    [WebhookInboxId] BIGINT IDENTITY(1,1) NOT NULL,
                    [CompanyId] INT NOT NULL,
                    [WhatsAppPhoneNumberId] INT NOT NULL,
                    [PhoneNumberId] NVARCHAR(100) NOT NULL,
                    [Status] NVARCHAR(50) NOT NULL DEFAULT N'PENDING',
                    [PayloadJson] NVARCHAR(MAX) NOT NULL,
                    [RetryCount] INT NOT NULL DEFAULT 0,
                    [LastError] NVARCHAR(MAX) NULL,
                    [ReceivedAtUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [LastAttemptAtUtc] DATETIME2 NULL,
                    [ProcessedAtUtc] DATETIME2 NULL,
                    [UpdatedAtUtc] DATETIME2 NULL,
                    CONSTRAINT [PK_WebhookInbox] PRIMARY KEY ([WebhookInboxId]),
                    CONSTRAINT [FK_WebhookInbox_Companies] FOREIGN KEY ([CompanyId]) REFERENCES [Companies]([CompanyId]) ON DELETE CASCADE
                );

                CREATE INDEX [IX_WebhookInbox_Status_RetryCount_ReceivedAtUtc]
                    ON [WebhookInbox]([Status], [RetryCount], [ReceivedAtUtc]);
                CREATE INDEX [IX_WebhookInbox_CompanyId_ReceivedAtUtc]
                    ON [WebhookInbox]([CompanyId], [ReceivedAtUtc] DESC);
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
        ["ConversationMessages_FileName"] = @"
            IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ConversationMessages') AND name = 'FileName')
            BEGIN
                ALTER TABLE [ConversationMessages] ADD [FileName] NVARCHAR(255) NULL;
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

    if (!await dbContext.Notifications.AnyAsync())
    {
        var unreadConversations = await dbContext.Conversations
            .AsNoTracking()
            .Where(x => x.UnreadCount > 0)
            .OrderByDescending(x => x.LastMessageAtUtc ?? x.CreatedAtUtc)
            .Take(200)
            .Select(x => new
            {
                x.CompanyId,
                x.ContactName,
                x.ContactNumber,
                x.LastMessageContent,
                x.LastMessageType,
                x.LastMessageAtUtc,
                x.CreatedAtUtc
            })
            .ToListAsync();

        var seededNotifications = unreadConversations
            .Select(x =>
            {
                var title = (x.ContactName ?? x.ContactNumber) ?? "New message";
                var body = string.IsNullOrWhiteSpace(x.LastMessageContent)
                    ? $"[{x.LastMessageType ?? "message"}]"
                    : x.LastMessageContent!;

                return new Notification
                {
                    CompanyId = x.CompanyId,
                    Type = "info",
                    Title = title.Length > 300 ? title[..300] : title,
                    Body = body.Length > 4000 ? body[..4000] : body,
                    Category = "inbox",
                    CreatedAtUtc = x.LastMessageAtUtc ?? x.CreatedAtUtc
                };
            })
            .ToList();

        if (seededNotifications.Count > 0)
        {
            dbContext.Notifications.AddRange(seededNotifications);
            await dbContext.SaveChangesAsync();
            Log.Information("Seeded {Count} inbox notifications from unread conversations.", seededNotifications.Count);
        }
    }
}
catch (Exception ex)
{
    Log.Warning(ex, "Database initialization failed — the server will start but some features may be unavailable until the DB is reachable.");
}

var configuredPathBase = builder.Configuration["PathBase"];
var normalizedPathBase = string.IsNullOrWhiteSpace(configuredPathBase)
    ? "/"
    : configuredPathBase.Trim();

if (!normalizedPathBase.StartsWith('/'))
{
    normalizedPathBase = "/" + normalizedPathBase;
}

if (normalizedPathBase.Length > 1 && normalizedPathBase.EndsWith('/'))
{
    normalizedPathBase = normalizedPathBase.TrimEnd('/');
}

app.UseForwardedHeaders();
if (!string.IsNullOrWhiteSpace(normalizedPathBase) && normalizedPathBase != "/")
{
    app.UsePathBase(normalizedPathBase);
}
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseSerilogRequestLogging();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseCors("DefaultCors");
app.UseRequestTimeouts();
app.UseRateLimiter();

app.UseAuthentication();
app.UseMiddleware<TenantSecurityMiddleware>();
app.UseAuthorization();
app.UseMiddleware<ApiLoggingMiddleware>();

var swaggerEnabled = app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:EnabledInProduction");
if (swaggerEnabled)
{
    app.UseSwagger(options =>
    {
        options.PreSerializeFilters.Add((swagger, request) =>
        {
            var serverUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
            swagger.Servers = [new Microsoft.OpenApi.Models.OpenApiServer { Url = serverUrl }];
        });
    });
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("./v1/swagger.json", "WhatsApp Cloud API v1");
        options.RoutePrefix = "swagger";
    });
}

app.MapHealthChecks("/health");
app.MapControllers();

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
