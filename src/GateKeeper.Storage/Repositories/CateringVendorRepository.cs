using Azure;
using Azure.Data.Tables;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Storage.Repositories;

public class CateringVendorRepository : ICateringVendorRepository
{
    private readonly TableClient _table;


    public CateringVendorRepository(
        IConfiguration configuration
    )
    {
        var connectionString =
            configuration["StorageConnection"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "StorageConnection configuration is not configured."
            );
        }

        _table = new TableClient(
            connectionString,
            "CateringVendors"
        );

        _table.CreateIfNotExists();
    }


    // ============================================================
    // ADD
    // ============================================================

    public async Task AddCateringVendorAsync(
        CateringVendorEntity cateringVendor
    )
    {
        await _table.AddEntityAsync(
            cateringVendor
        );
    }


    // ============================================================
    // GET ONE
    // ============================================================

    public async Task<CateringVendorEntity?> GetCateringVendorAsync(
        string eventId,
        string cateringPassNumber
    )
    {
        try
        {
            var response =
                await _table.GetEntityAsync<CateringVendorEntity>(
                    eventId,
                    cateringPassNumber
                );

            return response.Value;
        }
        catch (RequestFailedException ex)
            when (ex.Status == 404)
        {
            return null;
        }
    }


    // ============================================================
    // GET ALL FOR EVENT
    // ============================================================

    public async Task<List<CateringVendorEntity>>
        GetCateringVendorsByEventAsync(
            string eventId
        )
    {
        var results =
            new List<CateringVendorEntity>();

        await foreach (
            var entity in _table.QueryAsync<CateringVendorEntity>(
                x => x.PartitionKey == eventId
            )
        )
        {
            results.Add(entity);
        }

        return results;
    }


    // ============================================================
    // UPDATE
    // ============================================================

    public async Task UpdateCateringVendorAsync(
        CateringVendorEntity cateringVendor
    )
    {
        await _table.UpdateEntityAsync(
            cateringVendor,
            cateringVendor.ETag,
            TableUpdateMode.Replace
        );
    }
}