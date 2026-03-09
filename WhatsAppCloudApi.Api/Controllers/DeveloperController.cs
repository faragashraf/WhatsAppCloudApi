using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/developer")]
[Authorize]
public sealed class DeveloperController : ApiControllerBase
{
    private readonly ITenantContextAccessor _tenantContext;
    private readonly IConfiguration _configuration;

    public DeveloperController(ITenantContextAccessor tenantContext, IConfiguration configuration)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
    }

    [HttpGet("info")]
    public IActionResult GetDeveloperInfo()
    {
        var ctx = _tenantContext.GetRequiredContext();
        var baseUrl = $"{Request.Scheme}://{Request.Host}/api";

        var info = new
        {
            BaseUrl = baseUrl,
            Version = "1.0",
            AuthType = "Bearer JWT",
            CompanyId = ctx.CompanyId,
            Endpoints = new[]
            {
                new { Method = "POST", Path = "/api/whatsapp/messages/text", Description = "Send a text message" },
                new { Method = "POST", Path = "/api/whatsapp/messages/template", Description = "Send a template message" },
                new { Method = "POST", Path = "/api/whatsapp/messages/media", Description = "Send a media message" },
                new { Method = "GET", Path = "/api/contacts", Description = "List contacts" },
                new { Method = "POST", Path = "/api/contacts", Description = "Create a contact" },
                new { Method = "GET", Path = "/api/conversations", Description = "List conversations" },
                new { Method = "POST", Path = "/api/campaigns", Description = "Create a campaign" },
                new { Method = "GET", Path = "/api/notifications", Description = "List notifications" },
            },
            CodeSamples = new object[]
            {
                new { Language = "cURL", Code = "curl -X POST " + baseUrl + "/whatsapp/messages/text \\\n  -H \"Authorization: Bearer YOUR_TOKEN\" \\\n  -H \"Content-Type: application/json\" \\\n  -d '{\"to\": \"966500000000\", \"body\": \"Hello from WhatsApp Egypt!\"}'" },
                new { Language = "C#", Code = "var client = new HttpClient();\nclient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(\"Bearer\", token);\nvar response = await client.PostAsJsonAsync(\"" + baseUrl + "/whatsapp/messages/text\", new { to = \"966500000000\", body = \"Hello!\" });" },
                new { Language = "JavaScript", Code = "const response = await fetch('" + baseUrl + "/whatsapp/messages/text', {\n  method: 'POST',\n  headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },\n  body: JSON.stringify({ to: '966500000000', body: 'Hello!' })\n});" },
                new { Language = "Python", Code = "import requests\nresponse = requests.post('" + baseUrl + "/whatsapp/messages/text',\n  headers={'Authorization': f'Bearer {token}'},\n  json={'to': '966500000000', 'body': 'Hello!'})" },
            }
        };

        return Ok(ApiResponse<object>.Ok(info));
    }
}
