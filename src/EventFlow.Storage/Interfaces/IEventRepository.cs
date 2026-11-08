
using EventFlow.Storage.Entities;

namespace EventFlow.Storage.Interfaces;

public interface IEventRepository
{
    Task<List<EventEntity>> GetEventsAsync(
        string tenantId);

    Task<EventEntity?> GetEventAsync(
        string tenantId,
        string eventId);

    Task AddEventAsync(
        string tenantId,
        EventEntity entity);

    Task UpdateEventAsync(
        string tenantId,
        EventEntity entity);

    // Atomically reserves ticket numbers within this tenant's event.
    // Azure ETag concurrency protection prevents duplicate numbers.
    Task<int> ReserveTicketNumbersAsync(
        string tenantId,
        string eventId,
        int quantity);
}
