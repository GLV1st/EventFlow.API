using Azure;
using Azure.Data.Tables;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Storage.Repositories;

public class EventRepository : IEventRepository
{
    private readonly TableClient _table;


    public EventRepository(IConfiguration configuration)
    {
        var connectionString =
            configuration["StorageConnection"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "StorageConnection configuration is missing."
            );
        }

        _table = new TableClient(
            connectionString,
            "Events"
        );

        Console.WriteLine(
            $"EventRepository connected to: {_table.Uri}"
        );
    }


    // =====================================
    // GET ALL EVENTS
    // =====================================

    public async Task<List<EventEntity>> GetEventsAsync()
    {
        var events = new List<EventEntity>();

        await foreach (
            var entity in _table.QueryAsync<EventEntity>())
        {
            events.Add(entity);
        }

        return events;
    }


    // =====================================
    // GET EVENT
    // =====================================

    public async Task<EventEntity?> GetEventAsync(
        string eventId)
    {
        Console.WriteLine(
            "================================="
        );

        Console.WriteLine(
            "GET EVENT"
        );

        Console.WriteLine(
            $"Event ID: {eventId}"
        );

        Console.WriteLine(
            "PartitionKey: EVENT"
        );

        Console.WriteLine(
            $"RowKey: {eventId}"
        );

        try
        {
            var result =
                await _table.GetEntityAsync<EventEntity>(
                    "EVENT",
                    eventId
                );

            Console.WriteLine(
                "EVENT FOUND"
            );

            Console.WriteLine(
                $"Event Name: {result.Value.Name}"
            );

            Console.WriteLine(
                $"Ticket Prefix: {result.Value.TicketPrefix}"
            );

            Console.WriteLine(
                $"Next Ticket Number: {result.Value.NextTicketNumber}"
            );

            Console.WriteLine(
                $"ETag: {result.Value.ETag}"
            );

            Console.WriteLine(
                "================================="
            );

            return result.Value;
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine(
                "AZURE EVENT TABLE ERROR"
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


            // ---------------------------------
            // Only 404 means event doesn't exist
            // ---------------------------------

            if (ex.Status == 404)
            {
                Console.WriteLine(
                    "EVENT NOT FOUND"
                );

                return null;
            }


            // ---------------------------------
            // Anything else is a real error
            // ---------------------------------

            throw;
        }
    }


    // =====================================
    // ADD EVENT
    // =====================================

    public async Task AddEventAsync(
        EventEntity entity)
    {
        Console.WriteLine(
            "================================="
        );

        Console.WriteLine(
            "ADDING EVENT"
        );

        Console.WriteLine(
            $"PartitionKey: {entity.PartitionKey}"
        );

        Console.WriteLine(
            $"RowKey: {entity.RowKey}"
        );

        Console.WriteLine(
            $"Name: {entity.Name}"
        );

        Console.WriteLine(
            $"Prefix: {entity.TicketPrefix}"
        );

        try
        {
            await _table.AddEntityAsync(entity);

            Console.WriteLine(
                "EVENT CREATED"
            );
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine(
                "FAILED TO CREATE EVENT"
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

            throw;
        }

        Console.WriteLine(
            "================================="
        );
    }


    // =====================================
    // UPDATE EVENT
    // =====================================

    public async Task UpdateEventAsync(
        EventEntity entity)
    {
        Console.WriteLine(
            "================================="
        );

        Console.WriteLine(
            "UPDATING EVENT"
        );

        Console.WriteLine(
            $"Event ID: {entity.RowKey}"
        );

        Console.WriteLine(
            $"Next Ticket Number: {entity.NextTicketNumber}"
        );

        Console.WriteLine(
            $"ETag: {entity.ETag}"
        );


        try
        {
            await _table.UpdateEntityAsync(
                entity,
                ETag.All,
                TableUpdateMode.Replace
            );

            Console.WriteLine(
                "EVENT UPDATED"
            );
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine(
                "FAILED TO UPDATE EVENT"
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

            throw;
        }

        Console.WriteLine(
            "================================="
        );
    }
}