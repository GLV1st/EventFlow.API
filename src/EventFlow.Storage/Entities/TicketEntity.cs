
using Azure;
using Azure.Data.Tables;

namespace EventFlow.Storage.Entities;

public class TicketEntity : ITableEntity
{
    // =====================================
    // TENANT / EVENT IDENTITY
    // =====================================

    public string TenantId { get; set; } = "";

    public string EventId { get; set; } = "";

    // =====================================
    // AZURE TABLE KEYS
    // =====================================

    public string PartitionKey { get; set; } = "";

    public string RowKey { get; set; } = "";

    // =====================================
    // WOOCOMMERCE
    // =====================================

    public string OrderId { get; set; } = "";

    public string OrderItemId { get; set; } = "";

    public string TicketType { get; set; } = "";

    public string EventDate { get; set; } = "";

    // =====================================
    // CUSTOMER
    // =====================================

    public string CustomerName { get; set; } = "";

    public string CustomerEmail { get; set; } = "";

    // =====================================
    // TICKET STATUS
    // =====================================

    public bool CheckedIn { get; set; } = false;

    public DateTime? CheckedInAt { get; set; }

    public bool CheckedOut { get; set; } = false;

    public DateTime? CheckedOutAt { get; set; }

    public bool Cancelled { get; set; } = false;

    // =====================================
    // FIRST SCAN / ATTENDANCE
    // =====================================

    // Permanent record of the first successful entry scan.
    public DateTime? FirstScanAt { get; set; }

    // =====================================
    // AUDIT
    // =====================================

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime Updated { get; set; } = DateTime.UtcNow;

    // =====================================
    // AZURE
    // =====================================

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
