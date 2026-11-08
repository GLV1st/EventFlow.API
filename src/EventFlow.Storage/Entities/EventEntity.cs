
using Azure;
using Azure.Data.Tables;

namespace EventFlow.Storage.Entities;

public class EventEntity : ITableEntity
{
    // =====================================
    // TENANT IDENTITY
    // =====================================

    public string TenantId { get; set; } = "";

    // =====================================
    // AZURE TABLE KEYS
    // =====================================

    // PartitionKey = TenantId
    // RowKey = EventId
    public string PartitionKey { get; set; } = "";

    public string RowKey { get; set; } = "";

    // =====================================
    // EVENT DETAILS
    // =====================================

    public string Name { get; set; } = "";

    public string TicketPrefix { get; set; } = "";

    // Next available ticket number.
    public int NextTicketNumber { get; set; } = 1;

    // =====================================
    // AZURE METADATA
    // =====================================

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
