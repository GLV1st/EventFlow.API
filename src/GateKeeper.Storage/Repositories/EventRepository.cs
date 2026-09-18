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


    // =====================================
    // RESERVE TICKET NUMBERS
    // =====================================

    public async Task<int> ReserveTicketNumbersAsync(
        string eventId,
        int quantity)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ArgumentException(
                "EventId is required.",
                nameof(eventId)
            );
        }

        if (quantity <= 0)
        {
            throw new ArgumentException(
                "Quantity must be greater than zero.",
                nameof(quantity)
            );
        }


        // =====================================
        // RETRY FOR CONCURRENCY
        // =====================================

        const int maxAttempts = 10;


        for (int attempt = 1;
             attempt <= maxAttempts;
             attempt++)
        {
            try
            {
                // ---------------------------------
                // Read the latest event
                // ---------------------------------

                var result =
                    await _table.GetEntityAsync<EventEntity>(
                        "EVENT",
                        eventId
                    );

                var evt = result.Value;


                // ---------------------------------
                // Reserve the current block
                // ---------------------------------

                var firstNumber =
                    evt.NextTicketNumber;


                var nextNumber =
                    checked(
                        firstNumber + quantity
                    );


                evt.NextTicketNumber =
                    nextNumber;


                // ---------------------------------
                // IMPORTANT:
                //
                // Use the ETag we received from the
                // GET request.
                //
                // If another request changed the
                // event after our GET, Azure returns
                // HTTP 412 and we retry.
                // ---------------------------------

                await _table.UpdateEntityAsync(
                    evt,
                    result.Value.ETag,
                    TableUpdateMode.Replace
                );


                Console.WriteLine(
                    "================================="
                );

                Console.WriteLine(
                    "TICKET NUMBERS RESERVED"
                );

                Console.WriteLine(
                    $"Event ID: {eventId}"
                );

                Console.WriteLine(
                    $"First Number: {firstNumber}"
                );

                Console.WriteLine(
                    $"Quantity: {quantity}"
                );

                Console.WriteLine(
                    $"Next Number: {nextNumber}"
                );

                Console.WriteLine(
                    $"Attempt: {attempt}"
                );

                Console.WriteLine(
                    "================================="
                );


                // ---------------------------------
                // SUCCESS
                // ---------------------------------

                return firstNumber;
            }
            catch (RequestFailedException ex)
                when (ex.Status == 412)
            {
                // =================================
                // CONCURRENT UPDATE
                // =================================

                Console.WriteLine(
                    "================================="
                );

                Console.WriteLine(
                    "TICKET NUMBER CONCURRENCY CONFLICT"
                );

                Console.WriteLine(
                    $"Event ID: {eventId}"
                );

                Console.WriteLine(
                    $"Attempt: {attempt}"
                );

                Console.WriteLine(
                    "Another request changed the event."
                );

                Console.WriteLine(
                    "Retrying with the latest number..."
                );

                Console.WriteLine(
                    "================================="
                );


                // Small delay before retrying.
                await Task.Delay(
                    50 * attempt
                );
            }
        }


        // =====================================
        // FAILED AFTER RETRIES
        // =====================================

        throw new InvalidOperationException(
            $"Unable to reserve ticket numbers for event '{eventId}' because of repeated concurrent updates."
        );
    }
}