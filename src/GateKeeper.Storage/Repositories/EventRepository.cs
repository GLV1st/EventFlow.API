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
        var connectionString = configuration["StorageConnection"];

        _table = new TableClient(
            connectionString,
            "Events");
    }

    public async Task<List<EventEntity>> GetEventsAsync()
    {
        var events = new List<EventEntity>();

        await foreach (var entity in _table.QueryAsync<EventEntity>())
        {
            events.Add(entity);
        }

        return events;
    }

    public async Task<EventEntity?> GetEventAsync(string eventId)
    {
        try
        {
            var result = await _table.GetEntityAsync<EventEntity>("EVENT", eventId);
            return result.Value;
        }
        catch
        {
            return null;
        }
    }

    public async Task UpdateEventAsync(EventEntity entity)
    {
        await _table.UpdateEntityAsync(entity, entity.ETag);
    }
}