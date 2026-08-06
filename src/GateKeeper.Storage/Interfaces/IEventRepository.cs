using GateKeeper.Storage.Entities;

namespace GateKeeper.Storage.Interfaces;

public interface IEventRepository
{
    Task<List<EventEntity>> GetEventsAsync();

    Task<EventEntity?> GetEventAsync(string eventId);

    Task UpdateEventAsync(EventEntity entity);
}