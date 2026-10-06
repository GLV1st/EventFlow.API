using Azure;
using Azure.Data.Tables;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Storage.Repositories;

public class TraderPitchRepository : ITraderPitchRepository
{
    private readonly TableClient _table;

    public TraderPitchRepository(IConfiguration configuration)
    {
        var connectionString =
            configuration["StorageConnection"]
            ?? throw new InvalidOperationException(
                "StorageConnection is not configured.");

        _table = new TableClient(
            connectionString,
            "TraderPitches");
    }

    public async Task<TraderPitchEntity?> GetAsync(
        string eventId,
        string pitchId)
    {
        try
        {
            var response =
                await _table.GetEntityAsync<TraderPitchEntity>(
                    eventId,
                    pitchId);

            return response.Value;
        }
        catch (RequestFailedException ex)
            when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<TraderPitchEntity>> GetAllAsync(
        string eventId)
    {
        var results = new List<TraderPitchEntity>();

        await foreach (
            var pitch in _table.QueryAsync<TraderPitchEntity>(
                p => p.PartitionKey == eventId))
        {
            results.Add(pitch);
        }

        return results;
    }

    public async Task<IReadOnlyList<TraderPitchEntity>> GetAvailableAsync(
        string eventId)
    {
        var results = new List<TraderPitchEntity>();

        await foreach (
            var pitch in _table.QueryAsync<TraderPitchEntity>(
                p =>
                    p.PartitionKey == eventId &&
                    p.Status == "Available"))
        {
            results.Add(pitch);
        }

        return results;
    }

    public async Task UpsertAsync(
        TraderPitchEntity pitch)
    {
        await _table.UpsertEntityAsync(
            pitch,
            TableUpdateMode.Replace);
    }

    public async Task DeleteAsync(
        string eventId,
        string pitchId)
    {
        try
        {
            await _table.DeleteEntityAsync(
                eventId,
                pitchId);
        }
        catch (RequestFailedException ex)
            when (ex.Status == 404)
        {
            // Already deleted.
        }
    }

    public async Task AllocateAsync(
        IReadOnlyList<TraderPitchEntity> pitches)
    {
        if (pitches == null || pitches.Count == 0)
        {
            throw new ArgumentException(
                "At least one pitch is required.",
                nameof(pitches));
        }

        var actions = new List<TableTransactionAction>();

        foreach (var pitch in pitches)
        {
            actions.Add(
                new TableTransactionAction(
                    TableTransactionActionType.UpdateReplace,
                    pitch,
                    pitch.ETag));
        }

        await _table.SubmitTransactionAsync(actions);
    }

    public async Task ReserveAsync(
        IReadOnlyList<TraderPitchEntity> pitches)
    {
        if (pitches == null || pitches.Count == 0)
        {
            throw new ArgumentException(
                "At least one pitch is required.",
                nameof(pitches));
        }

        var actions = new List<TableTransactionAction>();

        foreach (var pitch in pitches)
        {
            actions.Add(
                new TableTransactionAction(
                    TableTransactionActionType.UpdateReplace,
                    pitch,
                    pitch.ETag));
        }

        await _table.SubmitTransactionAsync(actions);
    }
    public async Task ReallocateAsync(
    IReadOnlyList<TraderPitchEntity> oldPitches,
    IReadOnlyList<TraderPitchEntity> newPitches)
{
    if (oldPitches == null || oldPitches.Count == 0)
    {
        throw new ArgumentException(
            "At least one old pitch is required.",
            nameof(oldPitches));
    }

    if (newPitches == null || newPitches.Count == 0)
    {
        throw new ArgumentException(
            "At least one new pitch is required.",
            nameof(newPitches));
    }

    var actions = new List<TableTransactionAction>();

    // Release the old pitches
    foreach (var pitch in oldPitches)
    {
        actions.Add(
            new TableTransactionAction(
                TableTransactionActionType.UpdateReplace,
                pitch,
                pitch.ETag));
    }

    // Book the new pitches
    foreach (var pitch in newPitches)
    {
        actions.Add(
            new TableTransactionAction(
                TableTransactionActionType.UpdateReplace,
                pitch,
                pitch.ETag));
    }

    await _table.SubmitTransactionAsync(actions);
}
}