
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

    private const int PasswordIterations = 210_000;

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

        if (tenant == null ||
            tenant.RecordType != "TENANT" ||
            tenant.Status != "ACTIVE")
        {
            return null;
        }

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

        if (client == null ||
            client.RecordType != "CLIENT" ||
            client.Status != "ACTIVE" ||
            client.TenantId != token.TenantId)
        {
            return null;
        }

        var tenant = await _repository.GetTenantAsync(token.TenantId);

        if (tenant == null ||
            tenant.RecordType != "TENANT" ||
            tenant.Status != "ACTIVE")
        {
            return null;
        }

        return token.TenantId;
    }

    // =========================================
    // EVENT API TOKEN VALIDATION
    // Accepts both legacy WooCommerce client tokens and
    // individual TicketPilot staff access tokens.
    // =========================================

    public async Task<EventAccessPrincipal?> ValidateEventAccessTokenAsync(
        string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        // Prefer individual staff tokens so role information is retained.
        var user = await ValidateUserAccessTokenAsync(accessToken);

        if (user != null)
            return new EventAccessPrincipal(
                user.TenantId,
                user.UserId,
                user.Role,
                false);

        // Keep existing WooCommerce integration working during and after migration.
        var tenantId = await ValidateAccessTokenAsync(accessToken);

        if (!string.IsNullOrWhiteSpace(tenantId))
            return new EventAccessPrincipal(
                tenantId,
                null,
                "WEBSITE",
                true);

        return null;
    }

    // =========================================
    // SECURITY HELPERS
    // =========================================

    public async Task<string> GenerateSupplierActivationKeyAsync(string label)
    {
        var key = "TP-" + GenerateRandomValue(18);

        await _repository.CreateActivationKeyAsync(
            HashValue(key),
            string.IsNullOrWhiteSpace(label)
                ? "Customer activation"
                : label.Trim());

        return key;
    }

    public async Task<WebsiteActivationResult?> ActivateWebsiteAsync(
        string key,
        string siteUrl,
        string siteName)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            string.IsNullOrWhiteSpace(siteUrl) ||
            string.IsNullOrWhiteSpace(siteName) ||
            !Uri.TryCreate(siteUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var local =
            uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase);

        if (uri.Scheme != Uri.UriSchemeHttps && !local)
            return null;

        var tenantId = Guid.NewGuid().ToString("N");
        var clientId = "tp_" + GenerateRandomValue(16);
        var clientSecret = GenerateRandomValue(32);
        var appCode = "TP-APP-" + GenerateRandomValue(18);

        var result = await _repository.ActivateWebsiteAsync(
            HashValue(key.Trim()),
            tenantId,
            uri.GetLeftPart(UriPartial.Path).TrimEnd('/'),
            siteName.Trim(),
            clientId,
            HashValue(clientSecret),
            HashValue(appCode));

        if (!result.Success)
            return null;

        return new WebsiteActivationResult(
            tenantId,
            clientId,
            clientSecret,
            appCode);
    }

    // =========================================
    // REGISTER OWNER
    // =========================================

    public async Task<OwnerRegistrationResult?> RegisterOwnerAsync(
        string code,
        string displayName,
        string email,
        string password,
        string organisationName,
        string shortCode)
    {
        if (string.IsNullOrWhiteSpace(code) ||
            string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(organisationName) ||
            string.IsNullOrWhiteSpace(shortCode) ||
            password.Length < 12 ||
            password.Length > 256 ||
            displayName.Trim().Length > 120 ||
            organisationName.Trim().Length > 120 ||
            shortCode.Trim().Length < 2 || shortCode.Trim().Length > 20 ||
            !System.Text.RegularExpressions.Regex.IsMatch(shortCode.Trim(), "^[A-Za-z0-9-]{2,20}$"))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (normalizedEmail.Length > 254 ||
            !normalizedEmail.Contains('@') ||
            normalizedEmail.StartsWith('@') ||
            normalizedEmail.EndsWith('@'))
        {
            return null;
        }

        var codeHash = HashValue(code.Trim());

        // The tenant is read from the still-active code. The repository then
        // rechecks it and atomically consumes it with Owner creation.
        var pending = await _repository.GetPendingTenantForAppCodeAsync(codeHash);

        if (pending == null ||
            string.IsNullOrWhiteSpace(pending.TenantId))
        {
            return null;
        }

        var tenantId = pending.TenantId;
        var userId = HashValue(normalizedEmail);
        var passwordHash = HashPassword(password);

        var tenant = await _repository.RedeemAppConnectionCodeAndCreateOwnerAsync(
            codeHash,
            tenantId,
            userId,
            normalizedEmail,
            displayName.Trim(),
            passwordHash,
            organisationName.Trim(),
            shortCode.Trim().ToUpperInvariant());

        if (tenant == null)
            return null;

        var accessToken = GenerateRandomValue(32);
        var expiresAt = DateTimeOffset.UtcNow.Add(AccessTokenLifetime);

        await _repository.CreateUserAccessTokenAsync(
            HashValue(accessToken),
            tenantId,
            userId,
            normalizedEmail,
            "OWNER",
            expiresAt);

        return new OwnerRegistrationResult(
            tenantId,
            tenant.Name ?? "",
            tenant.SiteUrl ?? "",
            userId,
            displayName.Trim(),
            normalizedEmail,
            "OWNER",
            accessToken,
            "Bearer",
            (int)AccessTokenLifetime.TotalSeconds);
    }

    // =========================================
    // FIND ORGANISATIONS FOR EMAIL LOGIN
    // =========================================

    public async Task<IReadOnlyList<OrganisationLoginOption>>
        FindOrganisationsByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return Array.Empty<OrganisationLoginOption>();

        var normalizedEmail = email.Trim().ToLowerInvariant();

        if (normalizedEmail.Length > 254 ||
            !normalizedEmail.Contains('@') ||
            normalizedEmail.StartsWith('@') ||
            normalizedEmail.EndsWith('@'))
        {
            return Array.Empty<OrganisationLoginOption>();
        }

        var users = await _repository.FindUsersByEmailAsync(normalizedEmail);

        var organisations = new List<OrganisationLoginOption>();

        var processedTenantIds = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var user in users)
        {
            if (user == null ||
                user.RecordType != "USER" ||
                user.Status != "ACTIVE" ||
                !string.Equals(
                    user.Email,
                    normalizedEmail,
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(user.TenantId))
            {
                continue;
            }

            var tenantId = user.TenantId;

            if (!processedTenantIds.Add(tenantId))
                continue;

            var tenant = await _repository.GetTenantAsync(tenantId);

            if (tenant == null ||
                tenant.RecordType != "TENANT" ||
                tenant.Status != "ACTIVE")
            {
                continue;
            }

            organisations.Add(
                new OrganisationLoginOption(
                    tenantId,
                    tenant.Name ?? "Organisation",
                    tenant.ShortCode));
        }

        return organisations;
    }

    // =========================================
    // LOGIN USER
    // Existing password authentication retained.
    // =========================================

    public async Task<UserLoginResult?> LoginUserAsync(
        string tenantId,
        string email,
        string password)
    {
        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrEmpty(password) ||
            password.Length > 256)
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var userId = HashValue(normalizedEmail);

        var tenant = await _repository.GetTenantAsync(tenantId);

        if (tenant == null ||
            tenant.RecordType != "TENANT" ||
            tenant.Status != "ACTIVE")
        {
            return null;
        }

        var user = await _repository.GetUserAsync(tenantId, userId);

        if (user == null ||
            user.RecordType != "USER" ||
            user.Status != "ACTIVE" ||
            user.TenantId != tenantId ||
            user.Email != normalizedEmail ||
            string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !VerifyPassword(password, user.PasswordHash))
        {
            return null;
        }

        var token = GenerateRandomValue(32);

        await _repository.CreateUserAccessTokenAsync(
            HashValue(token),
            tenantId,
            userId,
            normalizedEmail,
            user.Role ?? "SCANNER",
            DateTimeOffset.UtcNow.Add(AccessTokenLifetime));

        return new UserLoginResult(
            tenantId,
            userId,
            user.DisplayName ?? "",
            normalizedEmail,
            user.Role ?? "SCANNER",
            token,
            "Bearer",
            (int)AccessTokenLifetime.TotalSeconds);
    }

    // =========================================
    // PASSWORD RESET
    // =========================================

    public async Task<IReadOnlyList<PasswordResetRequestResult>> RequestPasswordResetAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return Array.Empty<PasswordResetRequestResult>();
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (normalizedEmail.Length > 254 || !normalizedEmail.Contains('@') ||
            normalizedEmail.StartsWith('@') || normalizedEmail.EndsWith('@'))
            return Array.Empty<PasswordResetRequestResult>();

        var users = await _repository.FindUsersByEmailAsync(normalizedEmail);
        var results = new List<PasswordResetRequestResult>();
        foreach (var user in users)
        {
            if (user.RecordType != "USER" || user.Status != "ACTIVE" ||
                string.IsNullOrWhiteSpace(user.TenantId) || string.IsNullOrWhiteSpace(user.UserId)) continue;
            var tenant = await _repository.GetTenantAsync(user.TenantId);
            if (tenant == null || tenant.RecordType != "TENANT" || tenant.Status != "ACTIVE") continue;

            var token = "TP-RESET-" + GenerateRandomValue(32);
            await _repository.CreatePasswordResetTokenAsync(
                HashValue(token), user.TenantId, user.UserId, normalizedEmail,
                DateTimeOffset.UtcNow.AddMinutes(30));
            results.Add(new PasswordResetRequestResult(
                normalizedEmail, user.DisplayName ?? "TicketPilot user", tenant.Name ?? "your organisation", token));
        }
        return results;
    }

    public async Task<bool> CompletePasswordResetAsync(string resetToken, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(resetToken) || string.IsNullOrWhiteSpace(newPassword) ||
            newPassword.Length < 12 || newPassword.Length > 256) return false;
        return await _repository.ResetPasswordAsync(
            HashValue(resetToken.Trim()), HashPassword(newPassword));
    }

    // =========================================
    // VALIDATE USER ACCESS TOKEN
    // =========================================

    public async Task<UserPrincipal?> ValidateUserAccessTokenAsync(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
            return null;

        var token = await _repository.GetUserAccessTokenAsync(HashValue(accessToken));

        if (token == null ||
            string.IsNullOrWhiteSpace(token.TenantId) ||
            string.IsNullOrWhiteSpace(token.UserId) ||
            string.IsNullOrWhiteSpace(token.OwnerEmail))
        {
            return null;
        }

        var user = await _repository.GetUserAsync(token.TenantId, token.UserId);

        if (user == null ||
            user.Status != "ACTIVE" ||
            user.TenantId != token.TenantId)
        {
            return null;
        }

        var tenant = await _repository.GetTenantAsync(token.TenantId);

        if (tenant == null || tenant.Status != "ACTIVE")
            return null;

        return new UserPrincipal(
            token.TenantId,
            token.UserId,
            token.OwnerEmail,
            user.Role ?? token.Role ?? "SCANNER");
    }

    // =========================================
    // REVOKE USER ACCESS TOKEN
    // =========================================

    public Task RevokeUserAccessTokenAsync(string accessToken) =>
        _repository.RevokeUserAccessTokenAsync(HashValue(accessToken));

    // =========================================
    // CREATE USER INVITATION
    // =========================================

    public async Task<InvitationCreatedResult> CreateUserInvitationAsync(
        UserPrincipal actor,
        string email,
        string displayName,
        string role)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedRole = role.Trim().ToUpperInvariant();

        if (normalizedEmail.Length > 254 ||
            !normalizedEmail.Contains('@') ||
            normalizedEmail.StartsWith('@') ||
            normalizedEmail.EndsWith('@') ||
            string.IsNullOrWhiteSpace(displayName) ||
            displayName.Trim().Length > 120)
        {
            throw new ArgumentException(
                "A valid email address and display name are required.");
        }

        var allowed = actor.Role == "OWNER"
            ? new[] { "ADMINISTRATOR", "EVENT_MANAGER", "SCANNER" }
            : actor.Role == "ADMINISTRATOR"
                ? new[] { "EVENT_MANAGER", "SCANNER" }
                : Array.Empty<string>();

        if (!allowed.Contains(normalizedRole))
            throw new UnauthorizedAccessException(
                "You do not have permission to invite that role.");

        var users = await _repository.ListUsersAsync(actor.TenantId);

        if (users.Any(u =>
            u.Email == normalizedEmail &&
            u.Status == "ACTIVE"))
        {
            throw new InvalidOperationException(
                "A user with this email already exists.");
        }

        var pendingInvitations =
            await _repository.ListInvitationsAsync(actor.TenantId);

        if (pendingInvitations.Any(i => i.Email == normalizedEmail))
        {
            throw new InvalidOperationException(
                "A pending invitation already exists for this email address.");
        }

        var token = "TP-INV-" + GenerateRandomValue(32);

        await _repository.CreateInvitationAsync(
            HashValue(token),
            actor.TenantId,
            normalizedEmail,
            displayName.Trim(),
            normalizedRole,
            actor.UserId);

        return new InvitationCreatedResult(
            token,
            normalizedEmail,
            displayName.Trim(),
            normalizedRole,
            DateTimeOffset.UtcNow);
    }

    // =========================================
    // ACCEPT USER INVITATION
    // =========================================

    public async Task<UserLoginResult?> AcceptUserInvitationAsync(
        string invitationToken,
        string displayName,
        string password)
    {
        if (string.IsNullOrWhiteSpace(invitationToken) ||
            string.IsNullOrWhiteSpace(displayName) ||
            displayName.Trim().Length > 120 ||
            string.IsNullOrWhiteSpace(password) ||
            password.Length < 12 ||
            password.Length > 256)
        {
            return null;
        }

        var hash = HashValue(invitationToken.Trim());

        var invite = await _repository.GetInvitationAsync(hash);

        if (invite == null ||
            string.IsNullOrWhiteSpace(invite.TenantId) ||
            string.IsNullOrWhiteSpace(invite.Email) ||
            string.IsNullOrWhiteSpace(invite.Role))
        {
            return null;
        }

        var tenant = await _repository.GetTenantAsync(invite.TenantId);

        if (tenant == null || tenant.Status != "ACTIVE")
            return null;

        var userId = HashValue(invite.Email);

        var ok = await _repository.AcceptInvitationAsync(
            hash,
            invite.TenantId,
            userId,
            invite.Email,
            displayName.Trim(),
            HashPassword(password),
            invite.Role);

        if (!ok)
            return null;

        var access = GenerateRandomValue(32);

        await _repository.CreateUserAccessTokenAsync(
            HashValue(access),
            invite.TenantId,
            userId,
            invite.Email,
            invite.Role,
            DateTimeOffset.UtcNow.Add(AccessTokenLifetime));

        return new UserLoginResult(
            invite.TenantId,
            userId,
            displayName.Trim(),
            invite.Email,
            invite.Role,
            access,
            "Bearer",
            (int)AccessTokenLifetime.TotalSeconds);
    }

    // =========================================
    // LIST TENANT USERS
    // =========================================

    public async Task<IReadOnlyList<ManagedUser>> ListTenantUsersAsync(string tenantId)
    {
        var users = await _repository.ListUsersAsync(tenantId);

        return users.Select(u => new ManagedUser(
            u.UserId ?? "",
            u.Email ?? "",
            u.DisplayName ?? "",
            u.Role ?? "",
            u.Status ?? "",
            u.CreatedAt)).ToList();
    }

    // =========================================
    // LIST TENANT INVITATIONS
    // =========================================

    public async Task<IReadOnlyList<PendingInvitation>> ListTenantInvitationsAsync(string tenantId)
    {
        var invites = await _repository.ListInvitationsAsync(tenantId);

        return invites.Select(i => new PendingInvitation(
            i.Email ?? "",
            i.DisplayName ?? "",
            i.Role ?? "",
            i.CreatedAt)).ToList();
    }

    // =========================================
    // CHANGE USER ROLE
    // =========================================

    public async Task<bool> ChangeUserRoleAsync(
        UserPrincipal actor,
        string targetUserId,
        string role)
    {
        var r = role.Trim().ToUpperInvariant();

        if (actor.Role != "OWNER" ||
            actor.UserId == targetUserId ||
            !new[] { "ADMINISTRATOR", "EVENT_MANAGER", "SCANNER" }.Contains(r))
        {
            return false;
        }

        return await _repository.UpdateUserRoleAsync(
            actor.TenantId,
            targetUserId,
            r);
    }

    // =========================================
    // DEACTIVATE USER
    // =========================================

    public async Task<bool> DeactivateTenantUserAsync(
        UserPrincipal actor,
        string targetUserId)
    {
        if ((actor.Role != "OWNER" && actor.Role != "ADMINISTRATOR") ||
            actor.UserId == targetUserId)
        {
            return false;
        }

        if (actor.Role == "ADMINISTRATOR")
        {
            var target = await _repository.GetUserAsync(
                actor.TenantId,
                targetUserId);

            if (target == null ||
                (target.Role != "EVENT_MANAGER" && target.Role != "SCANNER"))
            {
                return false;
            }
        }

        return await _repository.DeactivateUserAsync(
            actor.TenantId,
            targetUserId);
    }

    // =========================================
    // REVOKE USER INVITATION
    // =========================================

    public async Task<bool> RevokeUserInvitationAsync(
        UserPrincipal actor,
        string invitationToken)
    {
        if ((actor.Role != "OWNER" && actor.Role != "ADMINISTRATOR") ||
            string.IsNullOrWhiteSpace(invitationToken))
        {
            return false;
        }

        return await _repository.RevokeInvitationAsync(
            HashValue(invitationToken.Trim()),
            actor.TenantId);
    }

    // =========================================
    // PASSWORD HELPERS
    // =========================================

    private static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);

        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            PasswordIterations,
            HashAlgorithmName.SHA256,
            32);

        return $"pbkdf2-sha256${PasswordIterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    private static bool VerifyPassword(string password, string stored)
    {
        try
        {
            var parts = stored.Split('$');

            if (parts.Length != 4 ||
                parts[0] != "pbkdf2-sha256" ||
                !int.TryParse(parts[1], out var iterations) ||
                iterations < 100_000 ||
                iterations > 1_000_000)
            {
                return false;
            }

            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length);

            return CryptographicOperations.FixedTimeEquals(
                actual,
                expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // =========================================
    // RANDOM VALUE
    // =========================================

    private static string GenerateRandomValue(int byteCount)
    {
        var bytes = RandomNumberGenerator.GetBytes(byteCount);

        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    // =========================================
    // HASH VALUE
    // =========================================

    private static string HashValue(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);

        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
