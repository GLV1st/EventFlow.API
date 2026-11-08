
using System.Security.Cryptography;
using System.Text;
using EventFlow.Storage.Interfaces;

namespace EventFlow.Api.Security;

public class TenantSecurityService : ITenantSecurityService
{
    private readonly ITenantRegistryRepository _repository;

    private static readonly TimeSpan RegistrationCodeLifetime =
        TimeSpan.FromMinutes(30);

    private static readonly TimeSpan AccessTokenLifetime =
        TimeSpan.FromMinutes(15);

    public TenantSecurityService(
        ITenantRegistryRepository repository)
    {
        _repository = repository;
    }

    // =========================================
    // GENERATE REGISTRATION CODE
    // =========================================

    public async Task<RegistrationCodeResult>
        GenerateRegistrationCodeAsync()
    {
        var code = "EF-" + GenerateRandomValue(24);

        var codeHash = HashValue(code);

        var expiresAt =
            DateTimeOffset.UtcNow.Add(RegistrationCodeLifetime);

        await _repository.CreateRegistrationCodeAsync(
            codeHash,
            expiresAt);

        return new RegistrationCodeResult(
            code,
            expiresAt);
    }

    // =========================================
    // REDEEM REGISTRATION CODE
    // =========================================

    public async Task<TenantRegistrationResult?>
        RedeemRegistrationCodeAsync(
            string code,
            string siteUrl,
            string siteName)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(siteUrl) ||
            string.IsNullOrWhiteSpace(siteName))
        {
            return null;
        }

        if (!Uri.TryCreate(
            siteUrl,
            UriKind.Absolute,
            out var siteUri))
        {
            return null;
        }

        var isLocalDevelopment =
            siteUri.Host.Equals(
                "localhost",
                StringComparison.OrdinalIgnoreCase) ||
            siteUri.Host.Equals(
                "127.0.0.1",
                StringComparison.OrdinalIgnoreCase);

        if (siteUri.Scheme != Uri.UriSchemeHttps &&
            !isLocalDevelopment)
        {
            return null;
        }

        var normalizedSiteUrl =
            siteUri.GetLeftPart(UriPartial.Path).TrimEnd('/');

        var normalizedSiteName = siteName.Trim();

        var tenantId = Guid.NewGuid().ToString("N");

        var clientId = "ef_" + GenerateRandomValue(16);

        var clientSecret = GenerateRandomValue(32);

        var codeHash = HashValue(code.Trim());

        var secretHash = HashValue(clientSecret);

        var redeemed =
            await _repository.RedeemRegistrationCodeAsync(
                codeHash,
                tenantId,
                normalizedSiteUrl,
                normalizedSiteName,
                clientId,
                secretHash);

        if (!redeemed)
        {
            return null;
        }

        return new TenantRegistrationResult(
            tenantId,
            clientId,
            clientSecret);
    }

    // =========================================
    // CREATE ACCESS TOKEN
    // =========================================

    public async Task<AccessTokenResult?>
        CreateAccessTokenAsync(
            string clientId,
            string clientSecret)
    {
        if (string.IsNullOrWhiteSpace(clientId) ||
            string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var client =
            await _repository.GetClientAsync(clientId);

        if (client == null ||
            client.RecordType != "CLIENT" ||
            client.Status != "ACTIVE" ||
            string.IsNullOrWhiteSpace(client.TenantId) ||
            string.IsNullOrWhiteSpace(client.SecretHash))
        {
            return null;
        }

        var tenant = await _repository.GetTenantAsync(client.TenantId);
        if (tenant == null || tenant.RecordType != "TENANT" || tenant.Status != "ACTIVE")
            return null;

        var suppliedHash = HashValue(clientSecret);

        var suppliedBytes = Encoding.UTF8.GetBytes(suppliedHash);
        var storedBytes = Encoding.UTF8.GetBytes(client.SecretHash);

        if (!CryptographicOperations.FixedTimeEquals(
            suppliedBytes,
            storedBytes))
        {
            return null;
        }

        var accessToken = GenerateRandomValue(32);

        var tokenHash = HashValue(accessToken);

        var expiresAt =
            DateTimeOffset.UtcNow.Add(AccessTokenLifetime);

        await _repository.CreateAccessTokenAsync(
            tokenHash,
            client.TenantId,
            clientId,
            expiresAt);

        return new AccessTokenResult(
            accessToken,
            "Bearer",
            (int)AccessTokenLifetime.TotalSeconds);
    }

    // =========================================
    // VALIDATE ACCESS TOKEN
    // =========================================

    public async Task<string?> ValidateAccessTokenAsync(
        string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        var tokenHash = HashValue(accessToken);

        var token =
            await _repository.GetAccessTokenAsync(tokenHash);

        if (token == null ||
            token.RecordType != "ACCESS_TOKEN" ||
            token.Status != "ACTIVE" ||
            string.IsNullOrWhiteSpace(token.TenantId) ||
            string.IsNullOrWhiteSpace(token.ClientId))
        {
            return null;
        }

        var client = await _repository.GetClientAsync(token.ClientId);
        if (client == null || client.RecordType != "CLIENT" ||
            client.Status != "ACTIVE" || client.TenantId != token.TenantId)
            return null;

        var tenant = await _repository.GetTenantAsync(token.TenantId);
        if (tenant == null || tenant.RecordType != "TENANT" || tenant.Status != "ACTIVE")
            return null;

        return token.TenantId;
    }

    // =========================================
    // SECURITY HELPERS
    // =========================================

    private static string GenerateRandomValue(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashValue(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);

        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
