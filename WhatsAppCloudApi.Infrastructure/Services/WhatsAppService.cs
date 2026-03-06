using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Configuration;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Logging;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Infrastructure.Services;

public sealed class WhatsAppService : IWhatsAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<WhatsAppService> _logger;
    private readonly WhatsAppOptions _options;

    public WhatsAppService(HttpClient httpClient, IOptions<WhatsAppOptions> options, ILogger<WhatsAppService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public Task<ApiResponse<GenericGraphResponse>> SendTextMessageAsync(SendTextMessageRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            recipient_type = "individual",
            to = request.To,
            type = "text",
            text = new { preview_url = request.PreviewUrl, body = request.Body }
        };

        return PostGraphAsync($"{_options.PhoneNumberId}/messages", payload, cancellationToken);
    }

    public Task<ApiResponse<GenericGraphResponse>> SendTemplateMessageAsync(SendTemplateMessageRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            to = request.To,
            type = "template",
            template = new
            {
                name = request.TemplateName,
                language = new { code = request.LanguageCode },
                components = request.Components
            }
        };

        return PostGraphAsync($"{_options.PhoneNumberId}/messages", payload, cancellationToken);
    }

    public Task<ApiResponse<GenericGraphResponse>> SendMediaMessageAsync(SendMediaMessageRequest request, CancellationToken cancellationToken = default)
    {
        var mediaPayload = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(request.MediaId))
        {
            mediaPayload["id"] = request.MediaId;
        }

        if (!string.IsNullOrWhiteSpace(request.Link))
        {
            mediaPayload["link"] = request.Link;
        }

        if (!string.IsNullOrWhiteSpace(request.Caption))
        {
            mediaPayload["caption"] = request.Caption;
        }

        if (!string.IsNullOrWhiteSpace(request.FileName))
        {
            mediaPayload["filename"] = request.FileName;
        }

        var payload = new Dictionary<string, object?>
        {
            ["messaging_product"] = "whatsapp",
            ["to"] = request.To,
            ["type"] = request.MediaType.ToLowerInvariant(),
            [request.MediaType.ToLowerInvariant()] = mediaPayload
        };

        return PostGraphAsync($"{_options.PhoneNumberId}/messages", payload, cancellationToken);
    }

    public async Task<ApiResponse<GenericGraphResponse>> UploadMediaAsync(UploadMediaRequest request, CancellationToken cancellationToken = default)
    {
        using var formData = new MultipartFormDataContent();
        var bytes = Convert.FromBase64String(request.Base64Data);
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(request.ContentType);

        formData.Add(new StringContent("whatsapp"), "messaging_product");
        formData.Add(fileContent, "file", request.FileName);

        return await SendAsync(HttpMethod.Post, $"{_options.PhoneNumberId}/media", formData, cancellationToken);
    }

    public Task<ApiResponse<GenericGraphResponse>> GetMediaUrlAsync(string mediaId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, mediaId, null, cancellationToken);

    public Task<ApiResponse<GenericGraphResponse>> DeleteMediaAsync(string mediaId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, mediaId, null, cancellationToken);

    public Task<ApiResponse<GenericGraphResponse>> MarkMessageAsReadAsync(MarkAsReadRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            status = "read",
            message_id = request.MessageId
        };

        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        return SendAsync(HttpMethod.Put, $"{_options.PhoneNumberId}/messages", content, cancellationToken);
    }

    public Task<ApiResponse<GenericGraphResponse>> GetPhoneNumberDetailsAsync(CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"{_options.PhoneNumberId}?fields=verified_name,display_phone_number,quality_rating,code_verification_status", null, cancellationToken);

    public Task<ApiResponse<GenericGraphResponse>> RegisterPhoneNumberAsync(RegisterPhoneNumberRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            pin = request.Pin
        };

        return PostGraphAsync($"{_options.PhoneNumberId}/register", payload, cancellationToken);
    }

    public Task<ApiResponse<GenericGraphResponse>> DeregisterPhoneNumberAsync(CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Post, $"{_options.PhoneNumberId}/deregister", null, cancellationToken);

    public Task<ApiResponse<GenericGraphResponse>> RequestVerificationCodeAsync(RequestVerificationCodeRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            code_method = request.CodeMethod,
            locale = request.Locale
        };

        return PostGraphAsync($"{_options.PhoneNumberId}/request_code", payload, cancellationToken);
    }

    public Task<ApiResponse<GenericGraphResponse>> VerifyCodeAsync(VerifyCodeRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            code = request.Code
        };

        return PostGraphAsync($"{_options.PhoneNumberId}/verify_code", payload, cancellationToken);
    }

    public Task<ApiResponse<GenericGraphResponse>> GetMessageTemplatesAsync(CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"{_options.BusinessAccountId}/message_templates", null, cancellationToken);

    public Task<ApiResponse<GenericGraphResponse>> CreateMessageTemplateAsync(CreateTemplateRequest request, CancellationToken cancellationToken = default)
        => PostGraphAsync($"{_options.BusinessAccountId}/message_templates", request, cancellationToken);

    public Task<ApiResponse<GenericGraphResponse>> DeleteMessageTemplateAsync(string templateId, CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Delete, $"{_options.BusinessAccountId}/message_templates?name={Uri.EscapeDataString(templateId)}", null, cancellationToken);

    public Task<ApiResponse<GenericGraphResponse>> GetBusinessProfileAsync(CancellationToken cancellationToken = default)
        => SendAsync(HttpMethod.Get, $"{_options.PhoneNumberId}/whatsapp_business_profile?fields=about,address,description,email,profile_picture_url,websites,vertical", null, cancellationToken);

    public Task<ApiResponse<GenericGraphResponse>> UpdateBusinessProfileAsync(UpdateBusinessProfileRequest request, CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            messaging_product = "whatsapp",
            about = request.About,
            address = request.Address,
            description = request.Description,
            email = request.Email,
            profile_picture_handle = request.ProfilePictureHandle,
            websites = request.Websites,
            vertical = request.Vertical
        };

        return PostGraphAsync($"{_options.PhoneNumberId}/whatsapp_business_profile", payload, cancellationToken);
    }

    public Task<ApiResponse<GenericGraphResponse>> SendGraphRequestAsync(GraphApiRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Method) || string.IsNullOrWhiteSpace(request.Path))
        {
            return Task.FromResult(ApiResponse<GenericGraphResponse>.Fail(
                "Method and path are required.",
                HttpStatusCode.BadRequest));
        }

        var method = new HttpMethod(request.Method.ToUpperInvariant());
        var path = BuildPath(request.Path, request.Query);

        HttpContent? content = null;
        if (request.Body is not null && method != HttpMethod.Get && method != HttpMethod.Head)
        {
            content = new StringContent(JsonSerializer.Serialize(request.Body, JsonOptions), Encoding.UTF8, "application/json");
        }

        return SendAsync(method, path, content, cancellationToken);
    }

    private static string BuildPath(string path, Dictionary<string, string?> query)
    {
        if (query.Count == 0)
        {
            return path;
        }

        var queryString = string.Join("&", query
            .Where(x => !string.IsNullOrWhiteSpace(x.Key))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value ?? string.Empty)}"));

        if (string.IsNullOrEmpty(queryString))
        {
            return path;
        }

        return path.Contains('?', StringComparison.Ordinal) ? $"{path}&{queryString}" : $"{path}?{queryString}";
    }

    private Task<ApiResponse<GenericGraphResponse>> PostGraphAsync(string path, object payload, CancellationToken cancellationToken)
    {
        var content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        return SendAsync(HttpMethod.Post, path, content, cancellationToken);
    }

    private async Task<ApiResponse<GenericGraphResponse>> SendAsync(HttpMethod method, string path, HttpContent? content, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path)
        {
            Content = content
        };

        string? requestBody = null;
        if (content is not null && content is not MultipartFormDataContent)
        {
            requestBody = LogSanitizer.MaskSensitive(await content.ReadAsStringAsync(cancellationToken));
        }

        _logger.LogInformation("WhatsApp API Request: {Method} {Path} Body: {Body}", method, LogSanitizer.MaskSensitive(path), requestBody ?? "<empty>");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var sanitizedResponse = LogSanitizer.MaskSensitive(responseContent);

        _logger.LogInformation("WhatsApp API Response: {StatusCode} {Body}", (int)response.StatusCode, sanitizedResponse);

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
}
