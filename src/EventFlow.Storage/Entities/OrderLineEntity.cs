using Azure;
using Azure.Data.Tables;

namespace EventFlow.Storage.Entities;

// Idempotency marker stored in the same Azure Table partition as its tickets.
// This allows the marker and all tickets for an order line to be committed atomically.
public class OrderLineEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";

    public string TenantId { get; set; } = "";
    public string EventId { get; set; } = "";
    public string OrderId { get; set; } = "";
    public string OrderItemId { get; set; } = "";
    public string TicketNumbersJson { get; set; } = "[]";
    public int Quantity { get; set; }
    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
