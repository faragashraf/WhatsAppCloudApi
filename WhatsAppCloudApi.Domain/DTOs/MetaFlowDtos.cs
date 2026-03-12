using System.ComponentModel.DataAnnotations;

namespace WhatsAppCloudApi.Domain.DTOs;

public sealed class MetaFlowCreateRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(20)]
    public List<string> Categories { get; set; } = ["OTHER"];

    [MaxLength(120)]
    public string? CloneFlowId { get; set; }

    [MaxLength(2000)]
    public string? EndpointUri { get; set; }
}

public sealed class MetaFlowUpdateMetadataRequest
{
    [MaxLength(200)]
    public string? Name { get; set; }

    [MaxLength(20)]
    public List<string>? Categories { get; set; }

    [MaxLength(2000)]
    public string? EndpointUri { get; set; }
}

public sealed class MetaFlowJsonUpsertRequest
{
    [Required, MinLength(2)]
    public string FlowJson { get; set; } = string.Empty;
}
