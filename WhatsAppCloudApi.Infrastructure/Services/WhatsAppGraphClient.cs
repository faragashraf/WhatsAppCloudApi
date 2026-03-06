using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Logging;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class WhatsAppGraphClient : IWhatsAppGraphClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppGraphClient> _logger;

    public WhatsAppGraphClient(HttpClient httpClient, ILogger<WhatsAppGraphClient> logger, IOptions<WhatsAppOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _ = options.Value;
    }

    public async Task<ApiResponse<GenericGraphResponse>> SendAsync(
        TenantWhatsAppConfig config,
        HttpMethod method,
        string path,
        HttpContent? content,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, NormalizePath(path))
        {
            Content = content
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AccessToken);

        string? requestBody = null;
        if (content is not null && content is not MultipartFormDataContent)
        {
            requestBody = LogSanitizer.MaskSensitive(await content.ReadAsStringAsync(cancellationToken));
        }

        _logger.LogInformation(
            "WhatsApp API Request: CompanyId={CompanyId} Method={Method} Path={Path} Body={Body}",
            config.CompanyId,
            method,
            LogSanitizer.MaskSensitive(path),
            requestBody ?? "<empty>");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var sanitizedResponse = LogSanitizer.MaskSensitive(responseContent);

        _logger.LogInformation(
            "WhatsApp API Response: CompanyId={CompanyId} StatusCode={StatusCode} Body={Body}",
            config.CompanyId,
            (int)response.StatusCode,
            sanitizedResponse);

        if (!response.IsSuccessStatusCode)
        {
            return ApiResponse<GenericGraphResponse>.Fail(
                "WhatsApp API call failed.",
                response.StatusCode,
                details: sanitizedResponse);
        }

        if (string.IsNullOrWhiteSpace(responseContent))
        {
            return ApiResponse<GenericGraphResponse>.Ok(new GenericGraphResponse(), "Success");
        }

        var contentType = response.Content.Headers.ContentType?.MediaType;
        var isJson = !string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("json", StringComparison.OrdinalIgnoreCase);

        GenericGraphResponse data;
        if (isJson)
        {
            try
            {
                data = JsonSerializer.Deserialize<GenericGraphResponse>(responseContent, JsonOptions) ?? new GenericGraphResponse();
            }
            catch (JsonException)
            {
                data = new GenericGraphResponse { RawContent = responseContent };
            }
        }
        else
        {
            data = new GenericGraphResponse { RawContent = responseContent };
        }

        return ApiResponse<GenericGraphResponse>.Ok(data, "Success");
    }

    private string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.TrimStart('/');
    }
}
