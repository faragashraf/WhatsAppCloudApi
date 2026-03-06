namespace WhatsAppCloudApi.Domain.Entities;

public sealed class ApiLog
{
    public long ApiLogId { get; set; }
    public int? CompanyId { get; set; }
    public int? CompanyUserId { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string? RequestBody { get; set; }
    public string? ResponseBody { get; set; }
    public int StatusCode { get; set; }
    public string? IpAddress { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Company? Company { get; set; }
    public CompanyUser? CompanyUser { get; set; }
}
