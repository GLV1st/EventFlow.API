using EventFlow.Storage.Entities;

namespace EventFlow.Storage.Interfaces;

public interface ITicketRepository
{
    Task AddTicketAsync(string tenantId, TicketEntity ticket);

    Task<IReadOnlyList<TicketEntity>?> GetTicketsForOrderLineAsync(
        string tenantId,
        string eventId,
        string orderId,
        string orderItemId);

    Task<IReadOnlyList<TicketEntity>> CreateTicketsForOrderLineAsync(
        string tenantId,
        string eventId,
        string orderId,
        string orderItemId,
        IReadOnlyList<TicketEntity> tickets);

    Task<TicketEntity?> GetTicketAsync(
        string tenantId,
        string eventId,
        string ticketNumber);

    Task<List<TicketEntity>> GetTicketsByEventAsync(
        string tenantId,
        string eventId);

    Task UpdateTicketAsync(string tenantId, TicketEntity ticket);
}
