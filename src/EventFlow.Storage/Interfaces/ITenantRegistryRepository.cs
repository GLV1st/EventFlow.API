
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

    Task CreateActivationKeyAsync(string keyHash, string keyLabel);
    Task<ActivationResult> ActivateWebsiteAsync(string keyHash, string tenantId,
        string siteUrl, string siteName, string clientId, string secretHash,
        string appCodeHash);
    Task<TenantRegistryEntity?> GetPendingTenantForAppCodeAsync(string codeHash);
    Task<TenantRegistryEntity?> RedeemAppConnectionCodeAndCreateOwnerAsync(
        string codeHash, string tenantId, string userId, string email,
        string displayName, string passwordHash);
    Task<TenantRegistryEntity?> GetUserAsync(string tenantId, string userId);
    Task<List<TenantRegistryEntity>> FindUsersByEmailAsync(string email);
    Task CreateUserAccessTokenAsync(string tokenHash, string tenantId, string userId,
        string email, string role, DateTimeOffset expiresAt);
    Task<TenantRegistryEntity?> GetUserAccessTokenAsync(string tokenHash);
    Task RevokeUserAccessTokenAsync(string tokenHash);
    Task CreateInvitationAsync(string tokenHash, string tenantId, string email, string displayName, string role, string invitedByUserId);
    Task<TenantRegistryEntity?> GetInvitationAsync(string tokenHash);
    Task<List<TenantRegistryEntity>> ListInvitationsAsync(string tenantId);
    Task<bool> AcceptInvitationAsync(string tokenHash, string tenantId, string userId, string email, string displayName, string passwordHash, string role);
    Task<List<TenantRegistryEntity>> ListUsersAsync(string tenantId);
    Task<bool> UpdateUserRoleAsync(string tenantId, string userId, string role);
    Task<bool> DeactivateUserAsync(string tenantId, string userId);
    Task<bool> RevokeInvitationAsync(string tokenHash, string tenantId);
}

public record ActivationResult(bool Success, string? Reason = null);
