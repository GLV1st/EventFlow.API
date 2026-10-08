using Azure.Data.Tables;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Storage.Repositories;

public class TicketScanLogRepository : ITicketScanLogRepository
{
    private readonly TableClient _table;

    public TicketScanLogRepository(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnection"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "StorageConnection configuration is missing."
            );
        }

        _table = new TableClient(
            connectionString,
            "TicketScanLog"
        );

        // Azure Table Storage will create the table automatically
        // the first time the API starts if it does not already exist.
        _table.CreateIfNotExists();

        Console.WriteLine(
            $"TicketScanLogRepository connected to: {_table.Uri}"
        );
    }

    public async Task AddScanAsync(TicketScanLogEntity scan)
    {
        await _table.AddEntityAsync(scan);
    }

    public async Task<List<TicketScanLogEntity>> GetScansByEventAsync(
        string eventId)
    {
        var scans = new List<TicketScanLogEntity>();

        await foreach (var scan in _table.QueryAsync<TicketScanLogEntity>(
            t => t.PartitionKey == eventId))
        {
            scans.Add(scan);
        }

        return scans;
    }
}
