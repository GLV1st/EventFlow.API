
using Azure;
using Azure.Data.Tables;

namespace EventFlow.Storage.Entities;

public class TicketScanLogEntity : ITableEntity
{
    // =====================================
    // TENANT / EVENT IDENTITY
    // =====================================

    public string TenantId { get; set; } = string.Empty;

    public string EventId { get; set; } = string.Empty;

    // =====================================
    // AZURE TABLE KEYS
    // =====================================

    // PartitionKey will be TenantId + EventId.
    public string PartitionKey { get; set; } = string.Empty;

    // Unique value for every scan.
    public string RowKey { get; set; } = string.Empty;

    // =====================================
    // SCAN DETAILS
    // =====================================

    public DateTime ScanTime { get; set; }

    // IN or OUT
    public string ScanType { get; set; } = string.Empty;

    // Ticket category, e.g. Adult or Under 14.
    public string TicketType { get; set; } = string.Empty;

    // =====================================
    // AZURE
    // =====================================

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}
