using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhatsAppCloudApi.Domain.Models;

public sealed class WebhookPayload
{
    [JsonPropertyName("object")]
    public string? Object { get; set; }

    [JsonPropertyName("entry")]
    public List<WebhookEntry> Entry { get; set; } = [];
}

public sealed class WebhookEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("changes")]
    public List<WebhookChange> Changes { get; set; } = [];
}

public sealed class WebhookChange
{
    [JsonPropertyName("field")]
    public string? Field { get; set; }

    [JsonPropertyName("value")]
    public WebhookValue? Value { get; set; }
}

public sealed class WebhookValue
{
    [JsonPropertyName("messaging_product")]
    public string? MessagingProduct { get; set; }
    [JsonPropertyName("messages")]
    public List<WebhookMessage>? Messages { get; set; }

    [JsonPropertyName("statuses")]
    public List<WebhookStatus>? Statuses { get; set; }

    [JsonPropertyName("contacts")]
    public List<WebhookContact>? Contacts { get; set; }

    [JsonPropertyName("metadata")]
    public WebhookMetadata? Metadata { get; set; }
}

public sealed class WebhookContact
{
    [JsonPropertyName("profile")]
    public WebhookContactProfile? Profile { get; set; }

    [JsonPropertyName("wa_id")]
    public string? WaId { get; set; }
}

public sealed class WebhookContactProfile
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }
}

public sealed class WebhookMetadata
{
    [JsonPropertyName("display_phone_number")]
    public string? DisplayPhoneNumber { get; set; }

    [JsonPropertyName("phone_number_id")]
    public string? PhoneNumberId { get; set; }
}

public sealed class WebhookMessage
{
    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("button")]
    public WebhookButtonReply? Button { get; set; }

    [JsonPropertyName("interactive")]
    public WebhookInteractiveReply? Interactive { get; set; }

    [JsonPropertyName("text")]
    public WebhookText? Text { get; set; }

    [JsonPropertyName("image")]
    public WebhookMedia? Image { get; set; }

    [JsonPropertyName("video")]
    public WebhookMedia? Video { get; set; }

    [JsonPropertyName("audio")]
    public WebhookMedia? Audio { get; set; }

    [JsonPropertyName("document")]
    public WebhookMedia? Document { get; set; }

    [JsonPropertyName("sticker")]
    public WebhookMedia? Sticker { get; set; }

    [JsonPropertyName("location")]
    public WebhookLocation? Location { get; set; }

    [JsonPropertyName("contacts")]
    public List<WebhookInboundContact>? Contacts { get; set; }

    [JsonPropertyName("reaction")]
    public WebhookReaction? Reaction { get; set; }

    [JsonPropertyName("order")]
    public WebhookOrder? Order { get; set; }

    [JsonPropertyName("system")]
    public WebhookSystemMessage? System { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; set; }
}

public sealed class WebhookButtonReply
{
    [JsonPropertyName("payload")]
    public string? Payload { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }
}

public sealed class WebhookInteractiveReply
{
    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("button_reply")]
    public WebhookInteractiveOptionReply? ButtonReply { get; set; }

    [JsonPropertyName("list_reply")]
    public WebhookInteractiveListReply? ListReply { get; set; }

    [JsonPropertyName("nfm_reply")]
    public WebhookInteractiveNfmReply? NfmReply { get; set; }
}

public class WebhookInteractiveOptionReply
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }
}

public sealed class WebhookInteractiveListReply : WebhookInteractiveOptionReply
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }
}

public sealed class WebhookInteractiveNfmReply
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("response_json")]
    public JsonElement? ResponseJsonRaw { get; set; }

    [JsonIgnore]
    public string? ResponseJson
    {
        get
        {
            if (!ResponseJsonRaw.HasValue)
            {
                return null;
            }

            var value = ResponseJsonRaw.Value;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Object => value.GetRawText(),
                JsonValueKind.Array => value.GetRawText(),
                JsonValueKind.Number => value.ToString(),
                JsonValueKind.True => bool.TrueString.ToLowerInvariant(),
                JsonValueKind.False => bool.FalseString.ToLowerInvariant(),
                JsonValueKind.Null => null,
                JsonValueKind.Undefined => null,
                _ => value.GetRawText()
            };
        }
    }
}

public sealed class WebhookText
{
    [JsonPropertyName("body")]
    public string? Body { get; set; }
}

public sealed class WebhookMedia
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonPropertyName("sha256")]
    public string? Sha256 { get; set; }

    [JsonPropertyName("caption")]
    public string? Caption { get; set; }

    [JsonPropertyName("filename")]
    public string? FileName { get; set; }
}

public sealed class WebhookLocation
{
    [JsonPropertyName("latitude")]
    public decimal? Latitude { get; set; }

    [JsonPropertyName("longitude")]
    public decimal? Longitude { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }
}

public sealed class WebhookReaction
{
    [JsonPropertyName("message_id")]
    public string? MessageId { get; set; }

    [JsonPropertyName("emoji")]
    public string? Emoji { get; set; }
}

public sealed class WebhookOrder
{
    [JsonPropertyName("catalog_id")]
    public string? CatalogId { get; set; }

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("product_items")]
    public List<WebhookOrderItem>? ProductItems { get; set; }
}

public sealed class WebhookOrderItem
{
    [JsonPropertyName("product_retailer_id")]
    public string? ProductRetailerId { get; set; }

    [JsonPropertyName("quantity")]
    public int? Quantity { get; set; }

    [JsonPropertyName("item_price")]
    public string? ItemPrice { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; }
}

public sealed class WebhookInboundContact
{
    [JsonPropertyName("name")]
    public WebhookInboundContactName? Name { get; set; }

    [JsonPropertyName("phones")]
    public List<WebhookInboundContactPhone>? Phones { get; set; }

    [JsonPropertyName("emails")]
    public List<WebhookInboundContactEmail>? Emails { get; set; }
}

public sealed class WebhookInboundContactName
{
    [JsonPropertyName("formatted_name")]
    public string? FormattedName { get; set; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; set; }
}

public sealed class WebhookInboundContactPhone
{
    [JsonPropertyName("phone")]
    public string? Phone { get; set; }

    [JsonPropertyName("wa_id")]
    public string? WaId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public sealed class WebhookInboundContactEmail
{
    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public sealed class WebhookSystemMessage
{
    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("new_wa_id")]
    public string? NewWaId { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("identity")]
    public string? Identity { get; set; }
}

public sealed class WebhookStatus
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("recipient_id")]
    public string? RecipientId { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("timestamp")]
    public string? Timestamp { get; set; }
}
