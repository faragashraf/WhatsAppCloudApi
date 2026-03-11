namespace WhatsAppCloudApi.Domain.Configuration;

public sealed class DataProtectionOptions
{
    public const string SectionName = "DataProtection";

    public string ApplicationName { get; set; } = "WhatsAppCloudApi";

    public string KeyRingPath { get; set; } = "App_Data/DataProtection-Keys";
}
