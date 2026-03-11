using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Infrastructure.Data;
using WhatsAppCloudApi.Infrastructure.Services;

namespace WhatsAppCloudApi.Infrastructure;

public static class DependencyInjection
{
    private static readonly IAsyncPolicy<HttpResponseMessage> RetryPolicy = CreateRetryPolicy();
    private static readonly IAsyncPolicy<HttpResponseMessage> NoOpPolicy = Policy.NoOpAsync<HttpResponseMessage>();

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null)));

        services.AddOptions<WhatsAppOptions>()
            .Bind(configuration.GetSection(WhatsAppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddScoped<ITenantContextAccessor, TenantContextAccessor>();
        services.AddScoped<ITenantWhatsAppConfigService, TenantWhatsAppConfigService>();
        services.AddScoped<ISubscriptionValidationService, SubscriptionValidationService>();
        services.AddScoped<IMessageDispatchService, MessageDispatchService>();
        services.AddScoped<IMessageQueueProcessor, MessageQueueProcessor>();
        services.AddScoped<IEmailCredentialProtector, EmailCredentialProtector>();
        services.AddScoped<IEmailQueueService, EmailQueueService>();
        services.AddScoped<IEmailQueueProcessor, EmailQueueProcessor>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IWhatsAppService, WhatsAppService>();
        services.AddScoped<IContactService, ContactService>();
        services.AddScoped<ICustomerConversationResolver, CustomerConversationResolver>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<IRoutingService, RoutingService>();
        services.AddScoped<ICampaignService, CampaignService>();
        services.AddScoped<IAutomationService, AutomationService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IMetaVerificationService, MetaVerificationService>();

        services.AddHttpClient("meta-graph", (sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        services.AddHttpClient<IWhatsAppGraphClient, WhatsAppGraphClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            })
            .AddPolicyHandler(request => ShouldRetryGraphRequest(request) ? RetryPolicy : NoOpPolicy);

        return services;
    }

    private static bool ShouldRetryGraphRequest(HttpRequestMessage request)
    {
        var method = request.Method;
        return method == HttpMethod.Get
            || method == HttpMethod.Head
            || method == HttpMethod.Options
            || method == HttpMethod.Delete;
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode == 429)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
