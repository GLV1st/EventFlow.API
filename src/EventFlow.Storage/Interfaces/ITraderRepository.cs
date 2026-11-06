using EventFlow.Storage.Entities;

namespace EventFlow.Storage.Interfaces;

public interface ITraderRepository
{
    Task AddTraderAsync(TraderEntity trader);

    Task<TraderEntity?> GetTraderAsync(
        string eventId,
        string traderPassNumber);

    Task<List<TraderEntity>> GetTradersByEventAsync(
        string eventId);

    Task UpdateTraderAsync(TraderEntity trader);
}