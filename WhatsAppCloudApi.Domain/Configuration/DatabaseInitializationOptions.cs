using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.Configuration;

public sealed class DatabaseInitializationOptions
{
    public const string SectionName = "DatabaseInitialization";

    [Range(1, 10, ErrorMessage = "DatabaseInitialization:MaxRetryCount must be between 1 and 10.")]
    public int MaxRetryCount { get; set; } = 5;

    [Range(1, 60, ErrorMessage = "DatabaseInitialization:RetryDelaySeconds must be between 1 and 60.")]
    public int RetryDelaySeconds { get; set; } = 5;

    public bool FailFastOnError { get; set; } = true;

    public bool LogPendingMigrations { get; set; } = true;
}
