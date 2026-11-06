using Azure;
using Azure.Data.Tables;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EventFlow.Storage.Repositories;

public class TraderRepository : ITraderRepository
{
    private readonly TableClient _table;

    public TraderRepository(IConfiguration configuration)
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
            "Traders"
        );

        Console.WriteLine(
            $"TraderRepository connected to: {_table.Uri}"
        );
    }


    // =====================================
    // ADD TRADER
    // =====================================

    public async Task AddTraderAsync(
        TraderEntity trader)
    {
        await _table.AddEntityAsync(trader);
    }


    // =====================================
    // GET TRADER
    // =====================================

    public async Task<TraderEntity?> GetTraderAsync(
        string eventId,
        string traderPassNumber)
    {
        Console.WriteLine(
            "================================="
        );

        Console.WriteLine(
            "GET TRADER"
        );

        Console.WriteLine(
            $"Event ID: {eventId}"
        );

        Console.WriteLine(
            $"Trader Pass: {traderPassNumber}"
        );

        Console.WriteLine(
            $"PartitionKey: {eventId}"
        );

        Console.WriteLine(
            $"RowKey: {traderPassNumber}"
        );

        try
        {
            var result =
                await _table.GetEntityAsync<TraderEntity>(
                    eventId,
                    traderPassNumber
                );

            Console.WriteLine(
                "TRADER FOUND"
            );

            Console.WriteLine(
                $"Company: {result.Value.CompanyName}"
            );

            Console.WriteLine(
                $"Contact: {result.Value.ContactName}"
            );

            Console.WriteLine(
                "================================="
            );

            return result.Value;
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine(
                "AZURE TRADER TABLE ERROR"
            );

            Console.WriteLine(
                $"Status: {ex.Status}"
            );

            Console.WriteLine(
                $"ErrorCode: {ex.ErrorCode}"
            );

            Console.WriteLine(
                $"Message: {ex.Message}"
            );

            Console.WriteLine(
                "================================="
            );

            // Trader does not exist
            if (ex.Status == 404)
            {
                return null;
            }

            // Anything else is a genuine error
            throw;
        }
    }


    // =====================================
    // GET TRADERS BY EVENT
    // =====================================

    public async Task<List<TraderEntity>> GetTradersByEventAsync(
        string eventId)
    {
        var traders = new List<TraderEntity>();

        await foreach (
            var trader in _table.QueryAsync<TraderEntity>(
                t => t.PartitionKey == eventId
            ))
        {
            traders.Add(trader);
        }

        return traders;
    }


    // =====================================
    // UPDATE TRADER
    // =====================================

    public async Task UpdateTraderAsync(
        TraderEntity trader)
    {
        await _table.UpdateEntityAsync(
            trader,
            trader.ETag
        );
    }
}