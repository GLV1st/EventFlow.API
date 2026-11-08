using Azure;
using Azure.Data.Tables;

namespace EventFlow.Storage.Entities;

public class TenantRegistryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "REGISTRY";
    public string RowKey { get; set; } = "";

    public string RecordType { get; set; } = "";

    public string? TenantId { get; set; }
    public string? SiteUrl { get; set; }
    public string? Name { get; set; }

    public string? ClientId { get; set; }
    public string? SecretHash { get; set; }
    public string? TokenHash { get; set; }

    public string? Status { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? RedeemedAt { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
