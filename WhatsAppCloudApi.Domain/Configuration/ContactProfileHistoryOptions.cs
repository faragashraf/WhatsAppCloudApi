using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.Configuration;

public sealed class ContactProfileHistoryOptions
{
    public const string SectionName = "ContactProfileHistory";

    [Range(50, 10000, ErrorMessage = "ContactProfileHistory:MaxRowsPerContact must be between 50 and 10000.")]
    public int MaxRowsPerContact { get; set; } = 600;

    [Range(10, 9999, ErrorMessage = "ContactProfileHistory:TargetRowsPerContact must be between 10 and 9999.")]
    public int TargetRowsPerContact { get; set; } = 400;

    public bool EnablePendingCompaction { get; set; } = true;
}
