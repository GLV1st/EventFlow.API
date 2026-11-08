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