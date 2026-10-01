using Azure;
using Azure.Data.Tables;

namespace GateKeeper.Storage.Entities;

public class TraderPitchEntity : ITableEntity
{
    // =====================================
    // AZURE TABLE KEYS
    // =====================================

    public string PartitionKey { get; set; } = "";

    public string RowKey { get; set; } = "";


    // =====================================
    // PITCH
    // =====================================

    // Example: A1, A2, B4 etc.
    public string PitchId { get; set; } = "";

    // Available / Requested / Allocated
    public string Status { get; set; } = "Available";


    // =====================================
    // EVENT
    // =====================================

    public string EventId { get; set; } = "";

    public string EventName { get; set; } = "";


    // =====================================
    // TRADER
    // =====================================

    public string TraderPassNumber { get; set; } = "";

    public string CompanyName { get; set; } = "";

    public string OrderId { get; set; } = "";


    // =====================================
    // PITCH DETAILS
    // =====================================

    // 4x4, 8x4, 12x4 etc.
    public string PitchSize { get; set; } = "";

    // Number of 4m x 4m squares occupied.
    public int SquaresUsed { get; set; } = 1;


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