using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/developer")]
[Authorize(Roles = "Admin")]
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
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api";

        var info = new
        {
            BaseUrl = baseUrl,
            Version = "1.0",
            AuthType = "Bearer JWT",
            CompanyId = ctx.CompanyId,
            Endpoints = new object[]
            {
                // ── Messaging ──
                new { Method = "POST", Path = "/api/whatsapp/messages/text", Description = "Send a text message", Category = "Messaging", RequestBody = "{ \"to\": \"966500000000\", \"body\": \"Hello!\" }" },
                new { Method = "POST", Path = "/api/whatsapp/messages/template", Description = "Send a template message", Category = "Messaging", RequestBody = "{ \"to\": \"966500000000\", \"templateName\": \"hello_world\", \"languageCode\": \"en_US\", \"components\": [] }" },
                new { Method = "POST", Path = "/api/whatsapp/messages/media", Description = "Send a media message (image, video, audio, document)", Category = "Messaging", RequestBody = "{ \"to\": \"966500000000\", \"type\": \"image\", \"mediaUrl\": \"https://example.com/photo.jpg\" }" },
                new { Method = "POST", Path = "/api/whatsapp/messages", Description = "Send a raw JSON message payload", Category = "Messaging", RequestBody = "{ \"messaging_product\": \"whatsapp\", \"to\": \"...\", \"type\": \"text\", \"text\": { \"body\": \"Hello\" } }" },
                new { Method = "POST", Path = "/api/whatsapp/messages/read", Description = "Mark a message as read", Category = "Messaging", RequestBody = "{ \"messageId\": \"wamid.xxx\" }" },
                new { Method = "POST", Path = "/api/whatsapp/typing-indicator", Description = "Send typing indicator with a raw Graph payload", Category = "Messaging", RequestBody = "{ \"messaging_product\": \"whatsapp\", \"status\": \"read\", \"message_id\": \"wamid.xxx\", \"typing_indicator\": { \"type\": \"text\" } }" },

                // ── Media ──
                new { Method = "POST", Path = "/api/whatsapp/media/upload", Description = "Upload media file (base64)", Category = "Media", RequestBody = "{ \"fileName\": \"photo.jpg\", \"contentType\": \"image/jpeg\", \"base64Data\": \"...\" }" },
                new { Method = "GET", Path = "/api/whatsapp/media/{mediaId}", Description = "Get media URL by ID", Category = "Media", RequestBody = (string?)null },
                new { Method = "DELETE", Path = "/api/whatsapp/media/{mediaId}", Description = "Delete media by ID", Category = "Media", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/media/download/{*mediaPath}", Description = "Download media passthrough", Category = "Media", RequestBody = (string?)null },

                // ── Templates ──
                new { Method = "GET", Path = "/api/whatsapp/templates", Description = "List all message templates", Category = "Templates", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/templates", Description = "Create a new message template", Category = "Templates", RequestBody = "{ \"name\": \"my_template\", \"language\": \"en_US\", \"category\": \"MARKETING\", \"components\": [...] }" },
                new { Method = "GET", Path = "/api/whatsapp/templates/{templateId}", Description = "Get template by ID", Category = "Templates", RequestBody = (string?)null },
                new { Method = "DELETE", Path = "/api/whatsapp/templates/{templateId}", Description = "Delete a message template", Category = "Templates", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/templates/search", Description = "Search templates by name", Category = "Templates", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/templates/namespace", Description = "Get template namespace", Category = "Templates", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/templates/{templateId}/edit", Description = "Edit an existing template", Category = "Templates", RequestBody = "{ \"components\": [...] }" },
                new { Method = "DELETE", Path = "/api/whatsapp/templates", Description = "Advanced template delete (bulk)", Category = "Templates", RequestBody = "{ \"name\": \"template_name\" }" },

                // ── Phone Number ──
                new { Method = "GET", Path = "/api/whatsapp/phone-number", Description = "Get phone number details", Category = "Phone", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/phone-numbers", Description = "List all phone numbers", Category = "Phone", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/phone-number/{phoneNumberId}", Description = "Get phone number by ID", Category = "Phone", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/phone-number/register", Description = "Register a phone number", Category = "Phone", RequestBody = "{ \"pin\": \"123456\" }" },
                new { Method = "POST", Path = "/api/whatsapp/phone-number/deregister", Description = "Deregister a phone number", Category = "Phone", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/phone-number/request-code", Description = "Request verification code", Category = "Phone", RequestBody = "{ \"codeMethod\": \"SMS\", \"language\": \"en\" }" },
                new { Method = "POST", Path = "/api/whatsapp/phone-number/verify-code", Description = "Verify phone number code", Category = "Phone", RequestBody = "{ \"code\": \"123456\" }" },
                new { Method = "POST", Path = "/api/whatsapp/phone-number/two-step", Description = "Set two-step verification PIN", Category = "Phone", RequestBody = "{ \"pin\": \"123456\" }" },
                new { Method = "POST", Path = "/api/whatsapp/phone-number/encryption", Description = "Set encryption key", Category = "Phone", RequestBody = "{ \"businessPublicKey\": \"...\" }" },
                new { Method = "GET", Path = "/api/whatsapp/phone-number/encryption", Description = "Get encryption key info", Category = "Phone", RequestBody = (string?)null },

                // ── Business Profile ──
                new { Method = "GET", Path = "/api/whatsapp/business-profile", Description = "Get business profile", Category = "Business", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/business-profile", Description = "Update business profile", Category = "Business", RequestBody = "{ \"about\": \"We are WhatsApp Cloud API\", \"address\": \"...\", \"description\": \"...\" }" },
                new { Method = "GET", Path = "/api/whatsapp/waba", Description = "Get WABA details", Category = "Business", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/waba/owned", Description = "Get owned WABAs", Category = "Business", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/waba/shared", Description = "Get shared WABAs", Category = "Business", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/business-portfolio", Description = "Get business portfolio", Category = "Business", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/business-compliance", Description = "Get business compliance info", Category = "Business", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/business-compliance", Description = "Submit compliance info", Category = "Business", RequestBody = "{ ... }" },

                // ── Webhooks ──
                new { Method = "GET", Path = "/api/whatsapp/waba/subscriptions", Description = "Get webhook subscriptions", Category = "Webhooks", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/waba/subscriptions", Description = "Create webhook subscription", Category = "Webhooks", RequestBody = "{ ... }" },
                new { Method = "DELETE", Path = "/api/whatsapp/waba/subscriptions", Description = "Delete webhook subscription", Category = "Webhooks", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/webhook/custom/dispatch", Description = "Custom webhook: render variables into a message and queue delivery (token via X-Webhook-Token header or ?token=...)", Category = "Webhooks", RequestBody = "{ \"to\": \"201000000000\", \"message\": \"Hi {{customer_name}}, order {{order_id|default:NA}} is {{status|upper}}.\", \"variables\": { \"order_id\": \"A-102\", \"status\": \"ready\" }, \"outsideWindowAction\": \"template\", \"outsideWindowTemplate\": { \"templateName\": \"order_update_v1\", \"languageCode\": \"en_US\" }, \"strictVariables\": true }" },

                // ── Flows ──
                new { Method = "GET", Path = "/api/whatsapp/flows", Description = "List all flows", Category = "Flows", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/flows", Description = "Create a new flow", Category = "Flows", RequestBody = "{ \"name\": \"my_flow\", \"categories\": [\"OTHER\"] }" },
                new { Method = "GET", Path = "/api/whatsapp/flows/{flowId}", Description = "Get flow by ID", Category = "Flows", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/flows/{flowId}", Description = "Update a flow", Category = "Flows", RequestBody = "{ ... }" },
                new { Method = "DELETE", Path = "/api/whatsapp/flows/{flowId}", Description = "Delete a flow", Category = "Flows", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/flows/{flowId}/publish", Description = "Publish a flow", Category = "Flows", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/flows/{flowId}/deprecate", Description = "Deprecate a flow", Category = "Flows", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/flows/{flowId}/assets", Description = "Get flow assets", Category = "Flows", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/flows/migrate", Description = "Migrate flows", Category = "Flows", RequestBody = "{ ... }" },

                // ── Other WhatsApp Features ──
                new { Method = "GET", Path = "/api/whatsapp/commerce-settings", Description = "Get commerce settings", Category = "Other", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/commerce-settings", Description = "Update commerce settings", Category = "Other", RequestBody = "{ ... }" },
                new { Method = "GET", Path = "/api/whatsapp/block-users", Description = "Get blocked users list", Category = "Other", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/block-users", Description = "Block a user", Category = "Other", RequestBody = "{ ... }" },
                new { Method = "DELETE", Path = "/api/whatsapp/block-users", Description = "Unblock a user", Category = "Other", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/qr-codes", Description = "List QR codes", Category = "Other", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/whatsapp/qr-codes", Description = "Create QR code", Category = "Other", RequestBody = "{ \"prefilled_message\": \"Hello!\" }" },
                new { Method = "GET", Path = "/api/whatsapp/analytics", Description = "Get analytics data", Category = "Other", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/whatsapp/billing/extendedcredits", Description = "Get billing extended credits", Category = "Other", RequestBody = (string?)null },

                // ── Graph Fallback ──
                new { Method = "POST", Path = "/api/whatsapp/graph", Description = "Generic Graph API request (structured)", Category = "Graph", RequestBody = "{ \"method\": \"GET\", \"path\": \"/{phoneNumberId}\", \"query\": {}, \"body\": {} }" },
                new { Method = "*", Path = "/api/whatsapp/graph/{*path}", Description = "Direct Graph API passthrough (any method)", Category = "Graph", RequestBody = (string?)null },

                // ── CRM: Contacts ──
                new { Method = "GET", Path = "/api/contacts", Description = "List contacts with search & pagination", Category = "CRM", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/contacts/{id}", Description = "Get contact by ID", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/contacts", Description = "Create a new contact", Category = "CRM", RequestBody = "{ \"name\": \"John Doe\", \"phoneNumber\": \"+966500000000\", \"email\": \"john@example.com\" }" },
                new { Method = "PUT", Path = "/api/contacts/{id}", Description = "Update an existing contact", Category = "CRM", RequestBody = "{ \"name\": \"John Updated\", \"phoneNumber\": \"+966500000000\" }" },
                new { Method = "DELETE", Path = "/api/contacts/{id}", Description = "Delete a contact", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/contacts/import", Description = "Import contacts in bulk", Category = "CRM", RequestBody = "{ \"contacts\": [ { \"name\": \"...\", \"phoneNumber\": \"...\" } ] }" },

                // ── CRM: Conversations ──
                new { Method = "GET", Path = "/api/conversations", Description = "List conversations with search", Category = "CRM", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/conversations/{id}/messages", Description = "Get messages for a conversation", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/conversations/{id}/messages", Description = "Send a message in a conversation", Category = "CRM", RequestBody = "{ \"messageType\": \"text\", \"content\": \"Hello!\" }" },
                new { Method = "POST", Path = "/api/conversations/{id}/read", Description = "Mark conversation as read", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/conversations/{id}/typing-indicator", Description = "Send typing indicator for a conversation using its latest inbound WhatsApp message", Category = "CRM", RequestBody = (string?)null },

                // ── CRM: Campaigns ──
                new { Method = "GET", Path = "/api/campaigns", Description = "List campaigns", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/campaigns", Description = "Create a campaign", Category = "CRM", RequestBody = "{ \"name\": \"Promo\", \"templateName\": \"promo_template\", \"phoneNumberId\": 1, \"contactIds\": [1,2,3] }" },
                new { Method = "PUT", Path = "/api/campaigns/{id}", Description = "Update a campaign", Category = "CRM", RequestBody = "{ \"name\": \"Updated Promo\" }" },
                new { Method = "POST", Path = "/api/campaigns/{id}/launch", Description = "Launch a campaign", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/campaigns/{id}/cancel", Description = "Cancel a campaign", Category = "CRM", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/campaigns/{id}/contacts", Description = "Get campaign contacts", Category = "CRM", RequestBody = (string?)null },

                // ── CRM: Automation ──
                new { Method = "GET", Path = "/api/automation", Description = "List automation rules", Category = "CRM", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/automation/{id}", Description = "Get automation rule by ID", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/automation", Description = "Create an automation rule", Category = "CRM", RequestBody = "{ \"name\": \"Welcome\", \"triggerType\": \"keyword\", \"triggerValue\": \"hello\", \"responseType\": \"text\", \"responseContent\": \"Welcome!\" }" },
                new { Method = "PUT", Path = "/api/automation/{id}", Description = "Update an automation rule", Category = "CRM", RequestBody = "{ \"name\": \"Updated Rule\" }" },
                new { Method = "DELETE", Path = "/api/automation/{id}", Description = "Delete an automation rule", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/automation/{id}/toggle", Description = "Toggle rule active/inactive", Category = "CRM", RequestBody = (string?)null },

                // ── CRM: Notifications ──
                new { Method = "GET", Path = "/api/notifications", Description = "List notifications", Category = "CRM", RequestBody = (string?)null },
                new { Method = "GET", Path = "/api/notifications/unread-count", Description = "Get unread notification count", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/notifications/{id}/read", Description = "Mark notification as read", Category = "CRM", RequestBody = (string?)null },
                new { Method = "POST", Path = "/api/notifications/read-all", Description = "Mark all notifications read", Category = "CRM", RequestBody = (string?)null },
                new { Method = "DELETE", Path = "/api/notifications/{id}", Description = "Delete a notification", Category = "CRM", RequestBody = (string?)null },
                new { Method = "DELETE", Path = "/api/notifications/all", Description = "Delete all notifications (optionally filter with ?isRead=true|false)", Category = "CRM", RequestBody = (string?)null },
            },
            CodeSamples = new object[]
            {
                new { Language = "cURL", Code = "curl -X POST " + baseUrl + "/whatsapp/messages/text \\\n  -H \"Authorization: Bearer YOUR_TOKEN\" \\\n  -H \"Content-Type: application/json\" \\\n  -d '{\"to\": \"966500000000\", \"body\": \"Hello from BotGlobal Services!\"}'" },
                new { Language = "C#", Code = "var client = new HttpClient();\nclient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(\"Bearer\", token);\nvar response = await client.PostAsJsonAsync(\"" + baseUrl + "/whatsapp/messages/text\", new { to = \"966500000000\", body = \"Hello!\" });" },
                new { Language = "JavaScript", Code = "const response = await fetch('" + baseUrl + "/whatsapp/messages/text', {\n  method: 'POST',\n  headers: { 'Authorization': 'Bearer ' + token, 'Content-Type': 'application/json' },\n  body: JSON.stringify({ to: '966500000000', body: 'Hello!' })\n});" },
                new { Language = "Python", Code = "import requests\nresponse = requests.post('" + baseUrl + "/whatsapp/messages/text',\n  headers={'Authorization': f'Bearer {token}'},\n  json={'to': '966500000000', 'body': 'Hello!'})" },
            }
        };

        return Ok(ApiResponse<object>.Ok(info));
    }
}
