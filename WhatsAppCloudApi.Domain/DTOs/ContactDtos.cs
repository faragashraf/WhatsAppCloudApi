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

    [MaxLength(4000)]
    public string? Tags { get; set; }

    [MaxLength(8000)]
    public string? CustomFields { get; set; }

    [MaxLength(100)]
    public string? Source { get; set; }

    [MaxLength(4000)]
    public string? Notes { get; set; }
}

public sealed class ContactImportRequest
{
    [Required, MaxLength(2_800_000)]
    public string CsvBase64 { get; set; } = string.Empty;
}

public sealed class ContactQueryParams
{
    [MaxLength(200)]
    public string? Search { get; set; }

    [MaxLength(100)]
    public string? Tag { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 25;
}
