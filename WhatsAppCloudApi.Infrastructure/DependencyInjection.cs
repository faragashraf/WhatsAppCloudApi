using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Infrastructure.Services;

namespace WhatsAppCloudApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<WhatsAppOptions>()
            .Bind(configuration.GetSection(WhatsAppOptions.SectionName))
            .ValidateDataAnnotations();

        // IHttpContextAccessor is registered in the API project (Program.cs).

        services.AddHttpClient<IWhatsAppService, WhatsAppService>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
                client.BaseAddress = new Uri(options.BaseUrl);
                // Do not set a permanent Authorization header here. We will forward the incoming Authorization header per request if present.
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.TryAddWithoutValidation("Content-Type", "application/json");
            })
            .AddHttpMessageHandler(sp => new ForwardAuthorizationHandler(sp.GetRequiredService<IHttpContextAccessor>(), sp.GetRequiredService<IOptions<WhatsAppOptions>>()))
            .AddPolicyHandler(GetRetryPolicy());

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => (int)msg.StatusCode == 429)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
