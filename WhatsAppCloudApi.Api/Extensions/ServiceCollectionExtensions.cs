using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Configuration;
using WhatsAppCloudApi.Api.Middleware;
using WhatsAppCloudApi.Shared.Responses;
using WhatsAppCloudApi.Api.Services;

namespace WhatsAppCloudApi.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddControllers()
            .AddJsonOptions(opts =>
            {
                opts.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
                opts.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
            });

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var partitionKey = GetPartitionKey(httpContext);
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 180,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy("auth", httpContext =>
            {
                var partitionKey = $"auth:{GetPartitionKey(httpContext)}";
                return RateLimitPartition.GetSlidingWindowLimiter(
                    partitionKey,
                    _ => new SlidingWindowRateLimiterOptions
                    {
                        PermitLimit = 12,
                        Window = TimeSpan.FromMinutes(1),
                        SegmentsPerWindow = 6,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy("webhook", httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetTokenBucketLimiter(
                    $"webhook:{ip}",
                    _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 300,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokensPerPeriod = 300,
                        AutoReplenishment = true
                    });
            });

            options.AddPolicy("upload", httpContext =>
            {
                var partitionKey = $"upload:{GetPartitionKey(httpContext)}";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    });
            });

            options.OnRejected = async (context, token) =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiter");

                logger.LogWarning(
                    "Rate limit exceeded for {Method} {Path} from {RemoteIp}.",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers["Retry-After"] = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString();
                }

                if (!context.HttpContext.Response.HasStarted)
                {
                    var response = ApiResponse<object>.Fail(
                        "Too many requests.",
                        System.Net.HttpStatusCode.TooManyRequests,
                        correlationId: context.HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString() ?? context.HttpContext.TraceIdentifier);
                    context.HttpContext.Response.ContentType = "application/json";
                    await context.HttpContext.Response.WriteAsJsonAsync(response, cancellationToken: token);
                }
            };
        });

        var useInMemoryWebhookStore = configuration.GetValue<bool>("WebhookStore:UseInMemory");
        if (useInMemoryWebhookStore)
        {
            services.AddSingleton<IWebhookStore, InMemoryWebhookStore>();
        }
        else
        {
            services.AddScoped<IWebhookStore, DatabaseWebhookStore>();
        }

        services.AddScoped<IWebhookInboxQueue, DatabaseWebhookInboxQueue>();
        services.AddScoped<IWebhookPayloadProcessor, WebhookPayloadProcessor>();
        services.AddScoped<IWebhookInboxProcessor, WebhookInboxProcessor>();

        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.EnableAnnotations();
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "WhatsApp Cloud API Integration",
                Version = "v1",
                Description = "Production-ready ASP.NET Core Web API for WhatsApp Cloud API integration."
            });

            var xmlFilename = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
            }
            // Add Bearer (JWT) support in Swagger
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
                        Scheme = "bearer",
                        Name = "Bearer",
                        In = ParameterLocation.Header
                    },
                    new string[] { }
                }
            });
        });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var errors = string.Join("; ", context.ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));

                var correlationId = context.HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
                    ?? context.HttpContext.TraceIdentifier;
                var response = ApiResponse<object>.Fail(
                    "Validation failed.",
                    System.Net.HttpStatusCode.BadRequest,
                    correlationId: correlationId,
                    details: errors);
                return new BadRequestObjectResult(response);
            };
        });

        return services;
    }

    private static string GetPartitionKey(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var companyId = context.User.FindFirst("CompanyId")?.Value ?? "na";
            var userId = context.User.FindFirst("UserId")?.Value ?? "na";
            return $"user:{companyId}:{userId}";
        }

        return $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
    }
}
