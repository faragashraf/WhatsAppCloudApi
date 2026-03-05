using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using WhatsAppCloudApi.Application.Interfaces;
using WhatsAppCloudApi.Domain.Models;
using WhatsAppCloudApi.Shared.Responses;

namespace WhatsAppCloudApi.Api.Controllers;

[ApiController]
[Route("api/whatsapp")]
public sealed class WhatsAppController : ApiControllerBase
{
    private readonly IWhatsAppService _whatsAppService;

    public WhatsAppController(IWhatsAppService whatsAppService)
    {
        _whatsAppService = whatsAppService;
    }

    /// <summary>
    /// Send a text message.
    /// </summary>
    /// <remarks>
    /// Sample request:
    /// {
    ///   "to": "201234567890",
    ///   "body": "Hello from API",
    ///   "previewUrl": false
    /// }
    /// </remarks>
    [HttpPost("messages/text")]
    [SwaggerOperation(Tags = ["Messages"])]
    [ProducesResponseType(typeof(ApiResponse<GenericGraphResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<GenericGraphResponse>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<GenericGraphResponse>), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendText([FromBody] SendTextMessageRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.SendTextMessageAsync(request, cancellationToken));

    /// <summary>
    /// Send a template message.
    /// </summary>
    [HttpPost("messages/template")]
    [SwaggerOperation(Tags = ["Messages"])]
    public async Task<IActionResult> SendTemplate([FromBody] SendTemplateMessageRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.SendTemplateMessageAsync(request, cancellationToken));

    /// <summary>
    /// Send a media message.
    /// </summary>
    [HttpPost("messages/media")]
    [SwaggerOperation(Tags = ["Messages"])]
    public async Task<IActionResult> SendMedia([FromBody] SendMediaMessageRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.SendMediaMessageAsync(request, cancellationToken));

    /// <summary>
    /// Upload media file.
    /// </summary>
    [HttpPost("media/upload")]
    [SwaggerOperation(Tags = ["Media"])]
    public async Task<IActionResult> UploadMedia([FromBody] UploadMediaRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.UploadMediaAsync(request, cancellationToken));

    /// <summary>
    /// Get media URL by media ID.
    /// </summary>
    [HttpGet("media/{mediaId}")]
    [SwaggerOperation(Tags = ["Media"])]
    public async Task<IActionResult> GetMediaUrl([FromRoute] string mediaId, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.GetMediaUrlAsync(mediaId, cancellationToken));

    /// <summary>
    /// Delete media by media ID.
    /// </summary>
    [HttpDelete("media/{mediaId}")]
    [SwaggerOperation(Tags = ["Media"])]
    public async Task<IActionResult> DeleteMedia([FromRoute] string mediaId, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.DeleteMediaAsync(mediaId, cancellationToken));

    /// <summary>
    /// Mark message as read.
    /// </summary>
    [HttpPost("messages/read")]
    [SwaggerOperation(Tags = ["Messages"])]
    public async Task<IActionResult> MarkRead([FromBody] MarkAsReadRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.MarkMessageAsReadAsync(request, cancellationToken));

    /// <summary>
    /// Get phone number details.
    /// </summary>
    [HttpGet("phone-number")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> GetPhoneNumberDetails(CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.GetPhoneNumberDetailsAsync(cancellationToken));

    /// <summary>
    /// Register phone number.
    /// </summary>
    [HttpPost("phone-number/register")]
    [SwaggerOperation(Tags = ["Phone Number"])]
    public async Task<IActionResult> RegisterPhoneNumber([FromBody] RegisterPhoneNumberRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.RegisterPhoneNumberAsync(request, cancellationToken));

    /// <summary>
    /// Get message templates.
    /// </summary>
    [HttpGet("templates")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> GetTemplates(CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.GetMessageTemplatesAsync(cancellationToken));

    /// <summary>
    /// Create message template.
    /// </summary>
    [HttpPost("templates")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> CreateTemplate([FromBody] CreateTemplateRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.CreateMessageTemplateAsync(request, cancellationToken));

    /// <summary>
    /// Delete message template.
    /// </summary>
    [HttpDelete("templates/{templateId}")]
    [SwaggerOperation(Tags = ["Templates"])]
    public async Task<IActionResult> DeleteTemplate([FromRoute] string templateId, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.DeleteMessageTemplateAsync(templateId, cancellationToken));

    /// <summary>
    /// Get business profile.
    /// </summary>
    [HttpGet("business-profile")]
    [SwaggerOperation(Tags = ["Business Profile"])]
    public async Task<IActionResult> GetBusinessProfile(CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.GetBusinessProfileAsync(cancellationToken));

    /// <summary>
    /// Update business profile.
    /// </summary>
    [HttpPost("business-profile")]
    [SwaggerOperation(Tags = ["Business Profile"])]
    public async Task<IActionResult> UpdateBusinessProfile([FromBody] UpdateBusinessProfileRequest request, CancellationToken cancellationToken)
        => ToActionResult(await _whatsAppService.UpdateBusinessProfileAsync(request, cancellationToken));
}
