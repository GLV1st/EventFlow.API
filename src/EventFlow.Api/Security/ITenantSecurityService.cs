
namespace EventFlow.Api.Security;

public interface ITenantSecurityService
{
    Task<RegistrationCodeResult> GenerateRegistrationCodeAsync();

    Task<TenantRegistrationResult?> RedeemRegistrationCodeAsync(
        string code,
        string siteUrl,
        string siteName);

    Task<AccessTokenResult?> CreateAccessTokenAsync(
        string clientId,
        string clientSecret);

    Task<string?> ValidateAccessTokenAsync(string accessToken);

    Task<EventAccessPrincipal?> ValidateEventAccessTokenAsync(string accessToken);

    Task<string> GenerateSupplierActivationKeyAsync(string label);

    Task<WebsiteActivationResult?> ActivateWebsiteAsync(
        string key,
        string siteUrl,
        string siteName);

    Task<OwnerRegistrationResult?> RegisterOwnerAsync(
        string code,
        string displayName,
        string email,
        string password,
        string organisationName,
        string shortCode);

    // Finds the active organisations associated with an email address.
    Task<IReadOnlyList<OrganisationLoginOption>> FindOrganisationsByEmailAsync(
        string email);

    Task<UserLoginResult?> LoginUserAsync(
        string tenantId,
        string email,
        string password);

    Task<IReadOnlyList<PasswordResetRequestResult>> RequestPasswordResetAsync(string email);

    Task<bool> CompletePasswordResetAsync(string resetToken, string newPassword);

    Task<UserPrincipal?> ValidateUserAccessTokenAsync(string accessToken);

    Task RevokeUserAccessTokenAsync(string accessToken);

    Task<InvitationCreatedResult> CreateUserInvitationAsync(
        UserPrincipal actor,
        string email,
        string displayName,
        string role);

    Task<UserLoginResult?> AcceptUserInvitationAsync(
        string invitationToken,
        string displayName,
        string password);

    Task<IReadOnlyList<ManagedUser>> ListTenantUsersAsync(string tenantId);

    Task<IReadOnlyList<PendingInvitation>> ListTenantInvitationsAsync(string tenantId);

    Task<bool> ChangeUserRoleAsync(
        UserPrincipal actor,
        string targetUserId,
        string role);

    Task<bool> DeactivateTenantUserAsync(
        UserPrincipal actor,
        string targetUserId);

    Task<bool> RevokeUserInvitationAsync(
        UserPrincipal actor,
        string invitationToken);
}

public record RegistrationCodeResult(
    string Code,
    DateTimeOffset ExpiresAt);

public record TenantRegistrationResult(
    string TenantId,
    string ClientId,
    string ClientSecret);

public record AccessTokenResult(
    string AccessToken,
    string TokenType,
    int ExpiresIn);

public record WebsiteActivationResult(
    string TenantId,
    string ClientId,
    string ClientSecret,
    string AppConnectionCode);

public record OwnerRegistrationResult(
    string TenantId,
    string SiteName,
    string SiteUrl,
    string UserId,
    string DisplayName,
    string Email,
    string Role,
    string AccessToken,
    string TokenType,
    int ExpiresIn);

public record UserLoginResult(
    string TenantId,
    string UserId,
    string DisplayName,
    string Email,
    string Role,
    string AccessToken,
    string TokenType,
    int ExpiresIn);

public record OrganisationLoginOption(
    string TenantId,
    string OrganisationName,
    string? ShortCode = null);

public record UserPrincipal(
    string TenantId,
    string UserId,
    string Email,
    string Role);

public record EventAccessPrincipal(
    string TenantId,
    string? UserId,
    string Role,
    bool IsWebsiteClient);

public record InvitationCreatedResult(
    string InvitationToken,
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset CreatedAt);

public record ManagedUser(
    string UserId,
    string Email,
    string DisplayName,
    string Role,
    string Status,
    DateTimeOffset? CreatedAt);

public record PendingInvitation(
    string Email,
    string DisplayName,
    string Role,
    DateTimeOffset? CreatedAt);

public record PasswordResetRequestResult(
    string Email,
    string DisplayName,
    string OrganisationName,
    string ResetToken);
