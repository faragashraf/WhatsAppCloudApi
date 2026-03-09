using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

// ─── Contact DTOs ────────────────────────────────────────────
public sealed class ContactUpsertRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? Email { get; set; }

    public string? Tags { get; set; }
    public string? CustomFields { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
}

public sealed class ContactImportRequest
{
    [Required]
    public string CsvBase64 { get; set; } = string.Empty;
}

public sealed class ContactQueryParams
{
    public string? Search { get; set; }
    public string? Tag { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}
