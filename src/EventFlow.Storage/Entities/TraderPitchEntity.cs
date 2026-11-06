using Azure;
using Azure.Data.Tables;

namespace EventFlow.Storage.Entities;

public class TraderPitchEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";

    public string PitchId { get; set; } = "";

    public string Status { get; set; } = "Available";

    public string EventId { get; set; } = "";
    public string EventName { get; set; } = "";

    public string TraderPassNumber { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string OrderId { get; set; } = "";

    public string PitchSize { get; set; } = "";

    public int SquaresUsed { get; set; } = 1;

    // ==========================================
    // RESERVATION
    // ==========================================

    public string ReservationId { get; set; } = "";

    public DateTime? ReservedUntil { get; set; }

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public DateTime Updated { get; set; } = DateTime.UtcNow;

    public DateTimeOffset? Timestamp { get; set; }

    public ETag ETag { get; set; }
}