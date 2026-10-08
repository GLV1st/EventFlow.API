using Azure;
using Azure.Data.Tables;

namespace GateKeeper.Storage.Entities;

public class TicketScanLogEntity : ITableEntity
{
    // =====================================
    // AZURE TABLE KEYS
    // =====================================

    // Partitioned by EventId so reports can retrieve
    // all scans for one event efficiently.
    public string PartitionKey { get; set; } = string.Empty;

    // Unique value for every scan.
    public string RowKey { get; set; } = string.Empty;

    // =====================================
    // SCAN DETAILS
    // =====================================

    public DateTime ScanTime { get; set; }

    // IN or OUT
    public string ScanType { get; set; } = string.Empty;

    // Adult, Under 14, Trader, Catering, etc.
    public string TicketType { get; set; } = string.Empty;

    // =====================================
    // AZURE
    // =====================================

    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
}
