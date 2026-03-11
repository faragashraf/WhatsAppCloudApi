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

var isEfDesignTime = AppDomain.CurrentDomain.GetAssemblies()
    .Any(assembly => string.Equals(assembly.GetName().Name, "Microsoft.EntityFrameworkCore.Design", StringComparison.Ordinal));

if (string.IsNullOrWhiteSpace(jwtOptions.Key)
    || jwtOptions.Key.StartsWith("REPLACE_WITH_", StringComparison.OrdinalIgnoreCase)
    || jwtOptions.Key.StartsWith("__SET_", StringComparison.OrdinalIgnoreCase)
    || jwtOptions.Key.Length < 64)
{
    if (!isEfDesignTime)
    {
        throw new InvalidOperationException("Jwt:Key must be a non-placeholder secret with at least 64 characters (preferably from JWT__KEY env var).");
    }
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

// Initialize database via EF migrations and seed baseline data
try
{
    await app.InitializeDatabaseAsync();
}
catch (Exception ex)
{
    Log.Warning(ex, "Database initialization failed; the server will start but some features may be unavailable until the database is reachable.");
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
