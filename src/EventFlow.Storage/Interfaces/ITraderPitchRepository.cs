using EventFlow.Storage.Entities;

namespace EventFlow.Storage.Interfaces;

public interface ITraderPitchRepository
{
    Task<TraderPitchEntity?> GetAsync(string eventId, string pitchId);
    Task<IReadOnlyList<TraderPitchEntity>> GetAllAsync(string eventId);
    Task<IReadOnlyList<TraderPitchEntity>> GetAvailableAsync(string eventId);
    Task UpsertAsync(TraderPitchEntity pitch);
    Task DeleteAsync(string eventId, string pitchId);
    Task AllocateAsync(IReadOnlyList<TraderPitchEntity> pitches);
    Task ReserveAsync(IReadOnlyList<TraderPitchEntity> pitches);

    Task ReallocateAsync(
        IReadOnlyList<TraderPitchEntity> oldPitches,
        IReadOnlyList<TraderPitchEntity> newPitches);
}