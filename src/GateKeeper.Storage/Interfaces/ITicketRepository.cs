using GateKeeper.Storage.Entities;

namespace GateKeeper.Storage.Interfaces;

public interface ITicketRepository
{
    Task AddTicketAsync(TicketEntity ticket);

    Task<TicketEntity?> GetTicketAsync(string eventId, string ticketNumber);

    Task<List<TicketEntity>> GetTicketsByEventAsync(string eventId);

    Task UpdateTicketAsync(TicketEntity ticket);
}