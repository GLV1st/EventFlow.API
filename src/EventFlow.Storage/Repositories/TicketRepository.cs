using Azure;
using Azure.Data.Tables;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EventFlow.Storage.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly TableClient _table;

    public TicketRepository(IConfiguration configuration)
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
            "Tickets"
        );

        Console.WriteLine(
            $"TicketRepository connected to: {_table.Uri}"
        );
    }

    public async Task AddTicketAsync(TicketEntity ticket)
    {
        await _table.AddEntityAsync(ticket);
    }

    public async Task<TicketEntity?> GetTicketAsync(
        string eventId,
        string ticketNumber)
    {
        Console.WriteLine(
            "================================="
        );

        Console.WriteLine(
            "GET TICKET"
        );

        Console.WriteLine(
            $"Event ID: {eventId}"
        );

        Console.WriteLine(
            $"Ticket Number: {ticketNumber}"
        );

        Console.WriteLine(
            $"PartitionKey: {eventId}"
        );

        Console.WriteLine(
            $"RowKey: {ticketNumber}"
        );

        try
        {
            var result =
                await _table.GetEntityAsync<TicketEntity>(
                    eventId,
                    ticketNumber
                );

            Console.WriteLine(
                "TICKET FOUND"
            );

            Console.WriteLine(
                $"Customer: {result.Value.CustomerName}"
            );

            Console.WriteLine(
                $"Ticket Type: {result.Value.TicketType}"
            );

            Console.WriteLine(
                "================================="
            );

            return result.Value;
        }
        catch (RequestFailedException ex)
        {
            Console.WriteLine(
                "AZURE TABLE ERROR"
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

            // Genuine Azure Table 404
            if (ex.Status == 404)
            {
                return null;
            }

            // Anything else should NOT be hidden
            throw;
        }
    }

    public async Task<List<TicketEntity>> GetTicketsByEventAsync(
        string eventId)
    {
        var tickets = new List<TicketEntity>();

        await foreach (
            var ticket in _table.QueryAsync<TicketEntity>(
                t => t.PartitionKey == eventId
            ))
        {
            tickets.Add(ticket);
        }

        return tickets;
    }

    public async Task UpdateTicketAsync(
        TicketEntity ticket)
    {
        await _table.UpdateEntityAsync(
            ticket,
            ticket.ETag
        );
    }
}