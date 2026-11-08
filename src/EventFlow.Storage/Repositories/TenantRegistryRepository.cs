using Azure;
using Azure.Data.Tables;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Extensions.Configuration;

namespace EventFlow.Storage.Repositories;

public class TenantRegistryRepository : ITenantRegistryRepository
{
    private const string Partition = "REGISTRY";
    private const string TableName = "EventFlowRegistry";

    private readonly TableClient _table;

    public TenantRegistryRepository(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnection"];

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "StorageConnection configuration is missing.");
        }

        _table = new TableClient(connectionString, TableName);
    }

    private async Task EnsureTableAsync()
    {
        await _table.CreateIfNotExistsAsync();
    }

    public async Task CreateRegistrationCodeAsync(
        string codeHash,
        DateTimeOffset expiresAt)
    {
        await EnsureTableAsync();

        var entity = new TenantRegistryEntity
        {
            PartitionKey = Partition,
            RowKey = $"REGCODE_{codeHash}",
            RecordType = "REGISTRATION_CODE",
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };

        await _table.AddEntityAsync(entity);
    }

    public async Task<bool> RedeemRegistrationCodeAsync(
        string codeHash,
        string tenantId,
        string siteUrl,
        string siteName,
        string clientId,
        string secretHash)
    {
        await EnsureTableAsync();

        var codeRowKey = $"REGCODE_{codeHash}";

        var result = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition,
            codeRowKey);

        if (!result.HasValue || result.Value is null)
            return false;

        var registration = result.Value;

        if (registration.RecordType != "REGISTRATION_CODE" ||
            registration.Status != "ACTIVE" ||
            !registration.ExpiresAt.HasValue ||
            registration.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            return false;

        registration.Status = "REDEEMED";
        registration.RedeemedAt = DateTimeOffset.UtcNow;

        var tenant = new TenantRegistryEntity
        {
            PartitionKey = Partition,
            RowKey = $"TENANT_{tenantId}",
            RecordType = "TENANT",
            TenantId = tenantId,
            SiteUrl = siteUrl,
            Name = siteName,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var client = new TenantRegistryEntity
        {
            PartitionKey = Partition,
            RowKey = $"CLIENT_{clientId}",
            RecordType = "CLIENT",
            TenantId = tenantId,
            ClientId = clientId,
            SecretHash = secretHash,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow
        };

        try
        {
            await _table.SubmitTransactionAsync(
            [
                new TableTransactionAction(
                    TableTransactionActionType.UpdateReplace,
                    registration),

                new TableTransactionAction(
                    TableTransactionActionType.Add,
                    tenant),

                new TableTransactionAction(
                    TableTransactionActionType.Add,
                    client)
            ]);

            return true;
        }
        catch (RequestFailedException ex)
            when (ex.Status == 409 || ex.Status == 412)
        {
            return false;
        }
    }

    public async Task<TenantRegistryEntity?> GetClientAsync(
        string clientId)
    {
        await EnsureTableAsync();

        var result = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition,
            $"CLIENT_{clientId}");

        return result.HasValue && result.Value is not null
            ? result.Value
            : null;
    }

    public async Task<TenantRegistryEntity?> GetTenantAsync(string tenantId)
    {
        await EnsureTableAsync();

        var result = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition,
            $"TENANT_{tenantId}");

        return result.HasValue && result.Value is not null
            ? result.Value
            : null;
    }

    public async Task CreateAccessTokenAsync(
        string tokenHash,
        string tenantId,
        string clientId,
        DateTimeOffset expiresAt)
    {
        await EnsureTableAsync();

        var entity = new TenantRegistryEntity
        {
            PartitionKey = Partition,
            RowKey = $"TOKEN_{tokenHash}",
            RecordType = "ACCESS_TOKEN",
            TenantId = tenantId,
            ClientId = clientId,
            TokenHash = tokenHash,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        };

        await _table.AddEntityAsync(entity);
    }

    public async Task<TenantRegistryEntity?> GetAccessTokenAsync(
        string tokenHash)
    {
        await EnsureTableAsync();

        var result = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition,
            $"TOKEN_{tokenHash}");

        if (!result.HasValue || result.Value is null)
            return null;

        var token = result.Value;

        if (token.RecordType != "ACCESS_TOKEN" ||
            token.Status != "ACTIVE" ||
            !token.ExpiresAt.HasValue ||
            token.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            return null;

        return token;
    }
}
