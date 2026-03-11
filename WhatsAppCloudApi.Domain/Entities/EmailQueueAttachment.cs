namespace WhatsAppCloudApi.Domain.Entities;

public sealed class EmailQueueAttachment
{
    public long EmailQueueAttachmentId { get; set; }
    public long EmailQueueItemId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public string ContentBase64 { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public EmailQueueItem? EmailQueueItem { get; set; }
}
