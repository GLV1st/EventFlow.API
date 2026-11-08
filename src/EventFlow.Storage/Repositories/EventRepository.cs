
using Azure;
using Azure.Data.Tables;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EventFlow.Storage.Repositories;

public class EventRepository : IEventRepository
{
    private readonly TableClient _table;

    public EventRepository(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnection"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "StorageConnection configuration is missing.");
        }

        _table = new TableClient(connectionString, "Events");
    }

    private async Task EnsureTableAsync()
    {
        await _table.CreateIfNotExistsAsync();
    }

    private static void ValidateTenantId(string tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId is required.");
    }

    public async Task<List<EventEntity>> GetEventsAsync(
        string tenantId)
    {
        ValidateTenantId(tenantId);
        await EnsureTableAsync();

        var events = new List<EventEntity>();

        await foreach (var entity in _table.QueryAsync<EventEntity>(
            e => e.PartitionKey == tenantId))
        {
            events.Add(entity);
        }

        return events;
    }

    public async Task<EventEntity?> GetEventAsync(
        string tenantId,
        string eventId)
    {
        ValidateTenantId(tenantId);

        if (string.IsNullOrWhiteSpace(eventId))
            throw new ArgumentException("EventId is required.");

        await EnsureTableAsync();

        try
        {
            var result = await _table.GetEntityAsync<EventEntity>(
                tenantId,
                eventId);

            return result.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task AddEventAsync(
        string tenantId,
        EventEntity entity)
    {
        ValidateTenantId(tenantId);
        ArgumentNullException.ThrowIfNull(entity);

        if (!string.IsNullOrWhiteSpace(entity.TenantId) &&
            entity.TenantId != tenantId)
        {
            throw new InvalidOperationException(
                "Event tenant does not match the authenticated tenant.");
        }

        if (string.IsNullOrWhiteSpace(entity.RowKey))
            throw new ArgumentException("EventId is required.");

        entity.TenantId = tenantId;
        entity.PartitionKey = tenantId;

        await EnsureTableAsync();
        await _table.AddEntityAsync(entity);
    }

    public async Task UpdateEventAsync(
        string tenantId,
        EventEntity entity)
    {
        ValidateTenantId(tenantId);
        ArgumentNullException.ThrowIfNull(entity);

        if (!string.Equals(
            entity.TenantId,
            tenantId,
            StringComparison.Ordinal) ||
            !string.Equals(
                entity.PartitionKey,
                tenantId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Event tenant does not match the authenticated tenant.");
        }

        await EnsureTableAsync();

        await _table.UpdateEntityAsync(
            entity,
            entity.ETag,
            TableUpdateMode.Replace);
    }

    public async Task<int> ReserveTicketNumbersAsync(
        string tenantId,
        string eventId,
        int quantity)
    {
        ValidateTenantId(tenantId);

        if (string.IsNullOrWhiteSpace(eventId))
            throw new ArgumentException("EventId is required.");

        if (quantity <= 0)
            throw new ArgumentException(
                "Quantity must be greater than zero.");

        await EnsureTableAsync();

        const int maxAttempts = 10;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var result = await _table.GetEntityAsync<EventEntity>(
                    tenantId,
                    eventId);

                var evt = result.Value;

                if (evt.TenantId != tenantId ||
                    evt.PartitionKey != tenantId)
                {
                    throw new InvalidOperationException(
                        "Event tenant does not match the authenticated tenant.");
                }

                var firstNumber = evt.NextTicketNumber;
                var nextNumber = checked(firstNumber + quantity);

                evt.NextTicketNumber = nextNumber;

                await _table.UpdateEntityAsync(
                    evt,
                    result.Value.ETag,
                    TableUpdateMode.Replace);

                Console.WriteLine(
                    $"Reserved {quantity} ticket numbers for tenant {tenantId}, event {eventId}.");

                return firstNumber;
            }
            catch (RequestFailedException ex) when (ex.Status == 412)
            {
                Console.WriteLine(
                    $"Ticket number concurrency conflict. Attempt {attempt}.");

                await Task.Delay(50 * attempt);
            }
        }

        throw new InvalidOperationException(
            $"Unable to reserve ticket numbers for event '{eventId}' because of repeated concurrent updates.");
    }
}
