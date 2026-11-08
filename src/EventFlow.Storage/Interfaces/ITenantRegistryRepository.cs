
using EventFlow.Storage.Entities;

namespace EventFlow.Storage.Interfaces;

public interface ITenantRegistryRepository
{
    Task CreateRegistrationCodeAsync(
        string codeHash,
        DateTimeOffset expiresAt);

    Task<bool> RedeemRegistrationCodeAsync(
        string codeHash,
        string tenantId,
        string siteUrl,
        string siteName,
        string clientId,
        string secretHash);

    Task<TenantRegistryEntity?> GetClientAsync(
        string clientId);

    Task<TenantRegistryEntity?> GetTenantAsync(
        string tenantId);

    Task CreateAccessTokenAsync(
        string tokenHash,
        string tenantId,
        string clientId,
        DateTimeOffset expiresAt);

    Task<TenantRegistryEntity?> GetAccessTokenAsync(
        string tokenHash);
}
