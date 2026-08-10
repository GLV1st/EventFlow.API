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

        _table = new TableClient(
            connectionString,
            "Events");
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
        try
        {
            var result =
                await _table.GetEntityAsync<EventEntity>(
                    "EVENT",
                    eventId);

            return result.Value;
        }
        catch
        {
            return null;
        }
    }


    // =====================================
    // ADD EVENT
    // =====================================

    public async Task AddEventAsync(
        EventEntity entity)
    {
        await _table.AddEntityAsync(entity);
    }


    // =====================================
    // UPDATE EVENT
    // =====================================

    public async Task UpdateEventAsync(
        EventEntity entity)
    {
        await _table.UpdateEntityAsync(
            entity,
            entity.ETag);
    }
}