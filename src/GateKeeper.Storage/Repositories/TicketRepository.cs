using Azure.Data.Tables;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Storage.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly TableClient _table;

    public TicketRepository(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnection"];

        _table = new TableClient(
            connectionString,
            "Tickets");
    }

    public async Task AddTicketAsync(TicketEntity ticket)
    {
        await _table.AddEntityAsync(ticket);
    }

    public async Task<TicketEntity?> GetTicketAsync(string eventId, string ticketNumber)
    {
        try
        {
            return await _table.GetEntityAsync<TicketEntity>(eventId, ticketNumber);
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<TicketEntity>> GetTicketsByEventAsync(string eventId)
    {
        var tickets = new List<TicketEntity>();

        await foreach (var ticket in _table.QueryAsync<TicketEntity>(t => t.PartitionKey == eventId))
        {
            tickets.Add(ticket);
        }

        return tickets;
    }

    public async Task UpdateTicketAsync(TicketEntity ticket)
    {
        await _table.UpdateEntityAsync(ticket, ticket.ETag);
    }
}