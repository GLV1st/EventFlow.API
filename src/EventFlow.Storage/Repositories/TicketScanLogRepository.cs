
using Azure;
using Azure.Data.Tables;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EventFlow.Storage.Repositories;

public class TicketScanLogRepository : ITicketScanLogRepository
{
    private readonly TableClient _table;

    public TicketScanLogRepository(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnection"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "StorageConnection configuration is missing.");
        }

        _table = new TableClient(
            connectionString,
            "TicketScanLog");

        Console.WriteLine(
            $"TicketScanLogRepository connected to: {_table.Uri}");
    }

    private static string BuildPartitionKey(
        string tenantId,
        string eventId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId is required.");

        if (string.IsNullOrWhiteSpace(eventId))
            throw new ArgumentException("EventId is required.");

        if (tenantId.Contains('|') || eventId.Contains('|'))
            throw new ArgumentException(
                "TenantId and EventId cannot contain '|'.");
        
        return $"{tenantId}|{eventId}";
    }

    public async Task AddScanAsync(
        string tenantId,
        TicketScanLogEntity scan)
    {
        ArgumentNullException.ThrowIfNull(scan);

        var partitionKey = BuildPartitionKey(
            tenantId,
            scan.EventId);

        if (!string.IsNullOrWhiteSpace(scan.TenantId) &&
            scan.TenantId != tenantId)
        {
            throw new InvalidOperationException(
                "Scan tenant does not match the authenticated tenant.");
        }

        scan.TenantId = tenantId;
        scan.PartitionKey = partitionKey;

        if (string.IsNullOrWhiteSpace(scan.RowKey))
        {
            throw new ArgumentException(
                "Scan RowKey is required.");
        }

        await _table.CreateIfNotExistsAsync();
        await _table.AddEntityAsync(scan);
    }

    public async Task<List<TicketScanLogEntity>> GetScansByEventAsync(
        string tenantId,
        string eventId)
    {
        var partitionKey = BuildPartitionKey(tenantId, eventId);

        await _table.CreateIfNotExistsAsync();

        var scans = new List<TicketScanLogEntity>();

        await foreach (var scan in _table.QueryAsync<TicketScanLogEntity>(
            t => t.PartitionKey == partitionKey))
        {
            scans.Add(scan);
        }

        return scans;
    }
}
