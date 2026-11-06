using Azure;
using Azure.Data.Tables;

namespace EventFlow.Storage.Entities;

public class EventEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";

    public string Name { get; set; } = "";

    public string TicketPrefix { get; set; } = "";

    public int NextTicketNumber { get; set; } = 1;

    public int NextTraderNumber { get; set; } = 1;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}