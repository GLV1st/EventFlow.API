using Azure;
using Azure.Data.Tables;

namespace GateKeeper.Storage.Entities;

public class TicketEntity : ITableEntity
{
    // Azure Table Keys
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";

    // WooCommerce
    public string OrderId { get; set; } = "";
    public string TicketType { get; set; } = "";
    public string EventDate { get; set; } = "";

    // Customer
    public string CustomerName { get; set; } = "";
    public string CustomerEmail { get; set; } = "";

    // Ticket Status
    public bool CheckedIn { get; set; } = false;
    public DateTime? CheckedInAt { get; set; }

    public bool CheckedOut { get; set; } = false;
    public DateTime? CheckedOutAt { get; set; }

    public bool Cancelled { get; set; } = false;

    // Audit
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public DateTime Updated { get; set; } = DateTime.UtcNow;

    // Azure
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}