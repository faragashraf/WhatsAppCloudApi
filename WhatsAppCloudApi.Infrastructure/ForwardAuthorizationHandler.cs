using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using WhatsAppCloudApi.Domain.Configuration;

namespace WhatsAppCloudApi.Infrastructure;

internal sealed class ForwardAuthorizationHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;
    private readonly WhatsAppOptions _options;

    public ForwardAuthorizationHandler(IHttpContextAccessor accessor, IOptions<WhatsAppOptions> options)
    {
        _accessor = accessor;
        _options = options.Value;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var httpContext = _accessor.HttpContext;
            if (httpContext is not null && httpContext.Request.Headers.TryGetValue("Authorization", out var authValues))
            {
                var header = authValues.ToString();
                if (!string.IsNullOrEmpty(header))
                {
                    request.Headers.Remove("Authorization");
                    request.Headers.TryAddWithoutValidation("Authorization", header);
                }
            }
            else if (!string.IsNullOrEmpty(_options.AccessToken))
            {
                // fallback to configured access token
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AccessToken);
            }
        }
        catch
        {
            // swallow any errors and proceed; downstream call will fail if necessary
        }

        return base.SendAsync(request, cancellationToken);
    }
}
