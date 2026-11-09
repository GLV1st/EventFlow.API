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

    public async Task CreateActivationKeyAsync(string keyHash, string keyLabel)
    {
        await EnsureTableAsync();
        await _table.AddEntityAsync(new TenantRegistryEntity
        {
            PartitionKey = Partition,
            RowKey = $"ACTKEY_{keyHash}",
            RecordType = "SUPPLIER_ACTIVATION_KEY",
            Name = keyLabel,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    public async Task<ActivationResult> ActivateWebsiteAsync(
        string keyHash, string tenantId, string siteUrl, string siteName,
        string clientId, string secretHash, string appCodeHash)
    {
        await EnsureTableAsync();
        var keyResult = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition, $"ACTKEY_{keyHash}");
        if (!keyResult.HasValue || keyResult.Value is null)
            return new ActivationResult(false, "INVALID_KEY");

        var key = keyResult.Value;
        if (key.RecordType != "SUPPLIER_ACTIVATION_KEY" || key.Status != "ACTIVE")
            return new ActivationResult(false, "USED_OR_REVOKED_KEY");

        key.Status = "REDEEMED";
        key.RedeemedAt = DateTimeOffset.UtcNow;

        var now = DateTimeOffset.UtcNow;
        var tenant = new TenantRegistryEntity
        {
            PartitionKey = Partition, RowKey = $"TENANT_{tenantId}",
            RecordType = "TENANT", TenantId = tenantId, SiteUrl = siteUrl,
            Name = siteName, Status = "PENDING_OWNER", CreatedAt = now
        };
        var client = new TenantRegistryEntity
        {
            PartitionKey = Partition, RowKey = $"CLIENT_{clientId}",
            RecordType = "CLIENT", TenantId = tenantId, ClientId = clientId,
            SecretHash = secretHash, Status = "ACTIVE", CreatedAt = now
        };
        var appCode = new TenantRegistryEntity
        {
            PartitionKey = Partition, RowKey = $"APPCODE_{appCodeHash}",
            RecordType = "APP_CONNECTION_CODE", TenantId = tenantId,
            ClientId = clientId, CodeHash = appCodeHash, Status = "ACTIVE",
            CreatedAt = now, Name = siteName, SiteUrl = siteUrl
        };

        try
        {
            await _table.SubmitTransactionAsync([
                new TableTransactionAction(TableTransactionActionType.UpdateReplace, key),
                new TableTransactionAction(TableTransactionActionType.Add, tenant),
                new TableTransactionAction(TableTransactionActionType.Add, client),
                new TableTransactionAction(TableTransactionActionType.Add, appCode)
            ]);
            return new ActivationResult(true);
        }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
        {
            return new ActivationResult(false, "ALREADY_REDEEMED");
        }
    }

    public async Task<TenantRegistryEntity?> GetPendingTenantForAppCodeAsync(string codeHash)
    {
        await EnsureTableAsync();
        var result = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition, $"APPCODE_{codeHash}");
        if (!result.HasValue || result.Value is null) return null;
        var code = result.Value;
        if (code.RecordType != "APP_CONNECTION_CODE" || code.Status != "ACTIVE" ||
            string.IsNullOrWhiteSpace(code.TenantId)) return null;
        var tenant = await GetTenantAsync(code.TenantId);
        return tenant != null && tenant.Status == "PENDING_OWNER" ? tenant : null;
    }

    // Atomically consumes the app connection code, activates the tenant and creates
    // its first Owner. All records share the REGISTRY partition, so Azure Table
    // Storage can commit them in one transaction.
    public async Task<TenantRegistryEntity?> RedeemAppConnectionCodeAndCreateOwnerAsync(
        string codeHash, string tenantId, string userId, string email,
        string displayName, string passwordHash)
    {
        await EnsureTableAsync();
        var codeResult = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition, $"APPCODE_{codeHash}");
        if (!codeResult.HasValue || codeResult.Value is null) return null;

        var code = codeResult.Value;
        if (code.RecordType != "APP_CONNECTION_CODE" || code.Status != "ACTIVE" ||
            !string.Equals(code.TenantId, tenantId, StringComparison.Ordinal)) return null;

        var tenantResult = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition, $"TENANT_{tenantId}");
        if (!tenantResult.HasValue || tenantResult.Value is null) return null;
        var tenant = tenantResult.Value;
        if (tenant.RecordType != "TENANT" || tenant.Status != "PENDING_OWNER") return null;

        var now = DateTimeOffset.UtcNow;
        code.Status = "REDEEMED";
        code.RedeemedAt = now;
        tenant.Status = "ACTIVE";
        tenant.OwnerEmail = email;

        var owner = new TenantRegistryEntity
        {
            PartitionKey = Partition,
            RowKey = $"USER_{tenantId}_{userId}",
            RecordType = "USER",
            TenantId = tenantId,
            UserId = userId,
            Email = email,
            OwnerEmail = email,
            DisplayName = displayName,
            PasswordHash = passwordHash,
            Role = "OWNER",
            Status = "ACTIVE",
            CreatedAt = now
        };

        try
        {
            await _table.SubmitTransactionAsync([
                new TableTransactionAction(TableTransactionActionType.UpdateReplace, code),
                new TableTransactionAction(TableTransactionActionType.UpdateReplace, tenant),
                new TableTransactionAction(TableTransactionActionType.Add, owner)
            ]);
            return tenant;
        }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412)
        {
            return null;
        }
    }

    public async Task<TenantRegistryEntity?> GetUserAsync(string tenantId, string userId)
    {
        await EnsureTableAsync();
        var result = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition, $"USER_{tenantId}_{userId}");
        return result.HasValue && result.Value is not null ? result.Value : null;
    }

    public async Task CreateUserAccessTokenAsync(string tokenHash, string tenantId,
        string userId, string email, string role, DateTimeOffset expiresAt)
    {
        await EnsureTableAsync();
        await _table.AddEntityAsync(new TenantRegistryEntity
        {
            PartitionKey = Partition,
            RowKey = $"USERTOKEN_{tokenHash}",
            RecordType = "USER_ACCESS_TOKEN",
            TenantId = tenantId,
            UserId = userId,
            OwnerEmail = email,
            Role = role,
            TokenHash = tokenHash,
            Status = "ACTIVE",
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt
        });
    }

    public async Task<TenantRegistryEntity?> GetUserAccessTokenAsync(string tokenHash)
    {
        await EnsureTableAsync();
        var result = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition, $"USERTOKEN_{tokenHash}");
        if (!result.HasValue || result.Value is null) return null;
        var token = result.Value;
        if (token.RecordType != "USER_ACCESS_TOKEN" || token.Status != "ACTIVE" ||
            !token.ExpiresAt.HasValue || token.ExpiresAt.Value <= DateTimeOffset.UtcNow)
            return null;
        return token;
    }

    public async Task RevokeUserAccessTokenAsync(string tokenHash)
    {
        await EnsureTableAsync();
        var result = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(
            Partition, $"USERTOKEN_{tokenHash}");
        if (!result.HasValue || result.Value is null) return;
        var token = result.Value;
        if (token.RecordType != "USER_ACCESS_TOKEN" || token.Status != "ACTIVE") return;
        token.Status = "REVOKED";
        try
        {
            await _table.UpdateEntityAsync(token, token.ETag, TableUpdateMode.Replace);
        }
        catch (RequestFailedException ex) when (ex.Status == 404 || ex.Status == 412)
        {
            // Already revoked or concurrently changed; no further action required.
        }
    }


    // Invitations intentionally have no expiry. They remain valid until accepted or revoked.
    public async Task CreateInvitationAsync(string tokenHash, string tenantId, string email, string displayName, string role, string invitedByUserId)
    {
        await EnsureTableAsync();
        await _table.AddEntityAsync(new TenantRegistryEntity { PartitionKey = Partition, RowKey = $"INVITE_{tokenHash}", RecordType = "USER_INVITATION", TenantId = tenantId, Email = email, DisplayName = displayName, Role = role, UserId = invitedByUserId, TokenHash = tokenHash, Status = "PENDING", CreatedAt = DateTimeOffset.UtcNow });
    }

    public async Task<TenantRegistryEntity?> GetInvitationAsync(string tokenHash)
    {
        await EnsureTableAsync();
        var r = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(Partition, $"INVITE_{tokenHash}");
        return r.HasValue && r.Value?.RecordType == "USER_INVITATION" && r.Value.Status == "PENDING" ? r.Value : null;
    }

    public async Task<List<TenantRegistryEntity>> ListInvitationsAsync(string tenantId)
    {
        await EnsureTableAsync(); var list = new List<TenantRegistryEntity>();
        await foreach (var e in _table.QueryAsync<TenantRegistryEntity>(e => e.PartitionKey == Partition && e.RecordType == "USER_INVITATION" && e.TenantId == tenantId && e.Status == "PENDING")) list.Add(e);
        return list.OrderBy(x => x.CreatedAt).ToList();
    }

    public async Task<bool> AcceptInvitationAsync(string tokenHash, string tenantId, string userId, string email, string displayName, string passwordHash, string role)
    {
        await EnsureTableAsync();
        var r = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(Partition, $"INVITE_{tokenHash}");
        if (!r.HasValue || r.Value is null) return false;
        var invite = r.Value;
        if (invite.RecordType != "USER_INVITATION" || invite.Status != "PENDING" || invite.TenantId != tenantId || invite.Email != email || invite.Role != role) return false;
        var user = new TenantRegistryEntity { PartitionKey = Partition, RowKey = $"USER_{tenantId}_{userId}", RecordType = "USER", TenantId = tenantId, UserId = userId, Email = email, OwnerEmail = email, DisplayName = displayName, PasswordHash = passwordHash, Role = role, Status = "ACTIVE", CreatedAt = DateTimeOffset.UtcNow };
        invite.Status = "ACCEPTED"; invite.RedeemedAt = DateTimeOffset.UtcNow;
        try { await _table.SubmitTransactionAsync([new TableTransactionAction(TableTransactionActionType.UpdateReplace, invite), new TableTransactionAction(TableTransactionActionType.Add, user)]); return true; }
        catch (RequestFailedException ex) when (ex.Status == 409 || ex.Status == 412) { return false; }
    }

    public async Task<List<TenantRegistryEntity>> ListUsersAsync(string tenantId)
    {
        await EnsureTableAsync(); var list = new List<TenantRegistryEntity>();
        await foreach (var e in _table.QueryAsync<TenantRegistryEntity>(e => e.PartitionKey == Partition && e.RecordType == "USER" && e.TenantId == tenantId)) list.Add(e);
        return list.OrderBy(x => x.CreatedAt).ToList();
    }

    public async Task<bool> UpdateUserRoleAsync(string tenantId, string userId, string role)
    {
        await EnsureTableAsync(); var r = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(Partition, $"USER_{tenantId}_{userId}");
        if (!r.HasValue || r.Value is null) return false; var u = r.Value;
        if (u.RecordType != "USER" || u.TenantId != tenantId || u.Role == "OWNER" || u.Status != "ACTIVE") return false;
        u.Role = role;
        try { await _table.UpdateEntityAsync(u, u.ETag, TableUpdateMode.Replace); return true; }
        catch (RequestFailedException ex) when (ex.Status == 404 || ex.Status == 412) { return false; }
    }

    public async Task<bool> DeactivateUserAsync(string tenantId, string userId)
    {
        await EnsureTableAsync(); var r = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(Partition, $"USER_{tenantId}_{userId}");
        if (!r.HasValue || r.Value is null) return false; var u = r.Value;
        if (u.RecordType != "USER" || u.TenantId != tenantId || u.Role == "OWNER" || u.Status != "ACTIVE") return false;
        u.Status = "DISABLED";
        try { await _table.UpdateEntityAsync(u, u.ETag, TableUpdateMode.Replace); return true; }
        catch (RequestFailedException ex) when (ex.Status == 404 || ex.Status == 412) { return false; }
    }

    public async Task<bool> RevokeInvitationAsync(string tokenHash, string tenantId)
    {
        await EnsureTableAsync(); var r = await _table.GetEntityIfExistsAsync<TenantRegistryEntity>(Partition, $"INVITE_{tokenHash}");
        if (!r.HasValue || r.Value is null) return false; var invite = r.Value;
        if (invite.RecordType != "USER_INVITATION" || invite.Status != "PENDING" || invite.TenantId != tenantId) return false;
        invite.Status = "REVOKED";
        try { await _table.UpdateEntityAsync(invite, invite.ETag, TableUpdateMode.Replace); return true; }
        catch (RequestFailedException ex) when (ex.Status == 404 || ex.Status == 412) { return false; }
    }

}
