using Azure;
using Azure.Data.Tables;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace EventFlow.Storage.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly TableClient _table;

    public TicketRepository(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnection"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("StorageConnection configuration is missing.");

        _table = new TableClient(connectionString, "Tickets");
    }

    private async Task EnsureTableAsync() => await _table.CreateIfNotExistsAsync();

    private static string BuildPartitionKey(string tenantId, string eventId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId is required.");
        if (string.IsNullOrWhiteSpace(eventId))
            throw new ArgumentException("EventId is required.");
        if (tenantId.Contains('|') || eventId.Contains('|'))
            throw new ArgumentException("TenantId and EventId cannot contain '|'.");
        return $"{tenantId}|{eventId}";
    }

    private static string BuildOrderLineRowKey(string orderId, string orderItemId)
    {
        var identity = Encoding.UTF8.GetBytes($"{orderId.Trim()}|{orderItemId.Trim()}");
        var hash = Convert.ToHexString(SHA256.HashData(identity)).ToLowerInvariant();
        return $"ORDERLINE_{hash}";
    }

    public async Task AddTicketAsync(string tenantId, TicketEntity ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var partitionKey = BuildPartitionKey(tenantId, ticket.EventId);
        if (!string.IsNullOrWhiteSpace(ticket.TenantId) && ticket.TenantId != tenantId)
            throw new InvalidOperationException("Ticket tenant does not match the authenticated tenant.");
        ticket.TenantId = tenantId;
        ticket.PartitionKey = partitionKey;
        if (string.IsNullOrWhiteSpace(ticket.RowKey))
            throw new ArgumentException("Ticket number (RowKey) is required.");
        await EnsureTableAsync();
        await _table.AddEntityAsync(ticket);
    }

    public async Task<IReadOnlyList<TicketEntity>?> GetTicketsForOrderLineAsync(
        string tenantId,
        string eventId,
        string orderId,
        string orderItemId)
    {
        if (string.IsNullOrWhiteSpace(orderId) || string.IsNullOrWhiteSpace(orderItemId))
            throw new ArgumentException("OrderId and OrderItemId are required.");

        var partitionKey = BuildPartitionKey(tenantId, eventId);
        var markerRowKey = BuildOrderLineRowKey(orderId, orderItemId);
        await EnsureTableAsync();
        return await GetOrderLineTicketsAsync(partitionKey, markerRowKey);
    }

    public async Task<IReadOnlyList<TicketEntity>> CreateTicketsForOrderLineAsync(
        string tenantId,
        string eventId,
        string orderId,
        string orderItemId,
        IReadOnlyList<TicketEntity> tickets)
    {
        if (string.IsNullOrWhiteSpace(orderId))
            throw new ArgumentException("OrderId is required.");
        if (string.IsNullOrWhiteSpace(orderItemId))
            throw new ArgumentException("OrderItemId is required.");
        if (tickets is null || tickets.Count == 0 || tickets.Count > 99)
            throw new ArgumentException("An order line must contain between 1 and 99 tickets.");

        var partitionKey = BuildPartitionKey(tenantId, eventId);
        var markerRowKey = BuildOrderLineRowKey(orderId, orderItemId);
        await EnsureTableAsync();

        // Fast path: a previous webhook call already committed this order line.
        var existing = await GetOrderLineTicketsAsync(partitionKey, markerRowKey);
        if (existing is not null)
            return existing;

        var ticketNumbers = tickets.Select(t => t.RowKey).ToList();
        if (ticketNumbers.Any(string.IsNullOrWhiteSpace) || ticketNumbers.Distinct(StringComparer.Ordinal).Count() != ticketNumbers.Count)
            throw new ArgumentException("Ticket numbers must be present and unique.");

        foreach (var ticket in tickets)
        {
            if (!string.IsNullOrWhiteSpace(ticket.TenantId) && ticket.TenantId != tenantId)
                throw new InvalidOperationException("Ticket tenant does not match the authenticated tenant.");
            if (!string.IsNullOrWhiteSpace(ticket.EventId) && ticket.EventId != eventId)
                throw new InvalidOperationException("Ticket event does not match the requested event.");
            ticket.TenantId = tenantId;
            ticket.EventId = eventId;
            ticket.PartitionKey = partitionKey;
            ticket.OrderId = orderId.Trim();
            ticket.OrderItemId = orderItemId.Trim();
        }

        var marker = new OrderLineEntity
        {
            PartitionKey = partitionKey,
            RowKey = markerRowKey,
            TenantId = tenantId,
            EventId = eventId,
            OrderId = orderId.Trim(),
            OrderItemId = orderItemId.Trim(),
            TicketNumbersJson = JsonSerializer.Serialize(ticketNumbers),
            Quantity = tickets.Count,
            Created = DateTime.UtcNow
        };

        var actions = new List<TableTransactionAction>
        {
            new(TableTransactionActionType.Add, marker)
        };
        actions.AddRange(tickets.Select(t => new TableTransactionAction(TableTransactionActionType.Add, t)));

        try
        {
            // Azure Table transactions are atomic and limited to 100 actions, all in one partition.
            await _table.SubmitTransactionAsync(actions);
            return tickets;
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // A concurrent webhook retry may have won the marker insert. Return its committed tickets.
            var committed = await GetOrderLineTicketsAsync(partitionKey, markerRowKey);
            if (committed is not null)
                return committed;
            throw;
        }
    }

    private async Task<IReadOnlyList<TicketEntity>?> GetOrderLineTicketsAsync(
        string partitionKey, string markerRowKey)
    {
        try
        {
            var markerResult = await _table.GetEntityAsync<OrderLineEntity>(partitionKey, markerRowKey);
            var numbers = JsonSerializer.Deserialize<List<string>>(markerResult.Value.TicketNumbersJson) ?? new();
            var result = new List<TicketEntity>(numbers.Count);
            foreach (var number in numbers)
            {
                var ticket = await _table.GetEntityAsync<TicketEntity>(partitionKey, number);
                result.Add(ticket.Value);
            }
            return result;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<TicketEntity?> GetTicketAsync(string tenantId, string eventId, string ticketNumber)
    {
        var partitionKey = BuildPartitionKey(tenantId, eventId);
        if (string.IsNullOrWhiteSpace(ticketNumber))
            throw new ArgumentException("Ticket number is required.");
        await EnsureTableAsync();
        try
        {
            var result = await _table.GetEntityAsync<TicketEntity>(partitionKey, ticketNumber);
            return result.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<List<TicketEntity>> GetTicketsByEventAsync(string tenantId, string eventId)
    {
        var partitionKey = BuildPartitionKey(tenantId, eventId);
        await EnsureTableAsync();
        var tickets = new List<TicketEntity>();
        // Exclude idempotency marker rows from ticket counts and reports.
        await foreach (var ticket in _table.QueryAsync<TicketEntity>(
            t => t.PartitionKey == partitionKey))
        {
            if (!ticket.RowKey.StartsWith("ORDERLINE_", StringComparison.Ordinal))
                tickets.Add(ticket);
        }
        return tickets;
    }

    public async Task UpdateTicketAsync(string tenantId, TicketEntity ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        var expectedPartitionKey = BuildPartitionKey(tenantId, ticket.EventId);
        if (!string.Equals(ticket.TenantId, tenantId, StringComparison.Ordinal))
            throw new InvalidOperationException("Ticket tenant does not match the authenticated tenant.");
        if (!string.Equals(ticket.PartitionKey, expectedPartitionKey, StringComparison.Ordinal))
            throw new InvalidOperationException("Ticket partition does not match the authenticated tenant and event.");
        await EnsureTableAsync();
        await _table.UpdateEntityAsync(ticket, ticket.ETag);
    }
}
