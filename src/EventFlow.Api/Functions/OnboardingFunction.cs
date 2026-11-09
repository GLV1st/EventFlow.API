using EventFlow.Api.Security;
using EventFlow.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class OnboardingFunction
{
    private readonly ITenantSecurityService _security;
    private readonly TicketPilotEmailService _email;

    public OnboardingFunction(ITenantSecurityService security, TicketPilotEmailService email)
    {
        _security = security;
        _email = email;
    }

    // Supplier-only operation. Requires the Azure Function host key.
    [Function("GenerateSupplierActivationKey")]
    public async Task<HttpResponseData> GenerateKey(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "admin/activation-keys")] HttpRequestData req)
    {
        var body = await req.ReadFromJsonAsync<ActivationKeyRequest>();
        var key = await _security.GenerateSupplierActivationKeyAsync(body?.Label ?? "Customer activation");
        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new { success = true, activationKey = key,
            message = "Store this key securely. It can only be redeemed once." });
        return response;
    }

    // Called by the WordPress plugin using the supplier activation key.
    [Function("ActivateWebsite")]
    public async Task<HttpResponseData> Activate(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/activate")] HttpRequestData req)
    {
        var body = await req.ReadFromJsonAsync<WebsiteActivationRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.ActivationKey) ||
            string.IsNullOrWhiteSpace(body.SiteUrl) || string.IsNullOrWhiteSpace(body.SiteName))
            return await Error(req, HttpStatusCode.BadRequest, "Activation key, website URL and website name are required.");

        var result = await _security.ActivateWebsiteAsync(body.ActivationKey, body.SiteUrl, body.SiteName);
        if (result == null)
            return await Error(req, HttpStatusCode.Unauthorized, "The activation key is invalid or has already been used.");

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new { success = true, result.TenantId,
            result.ClientId, result.ClientSecret, result.AppConnectionCode,
            message = "Website activated. Enter the app connection code in TicketPilot." });
        return response;
    }

    // Kept as a clear migration response so an older web app cannot consume a
    // connection code without creating the first Owner in the same operation.
    [Function("RedeemAppConnectionCode")]
    public async Task<HttpResponseData> RedeemAppCode(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/connect")] HttpRequestData req)
    {
        return await Error(req, HttpStatusCode.Gone,
            "This connection method has been replaced. Use POST /api/app/register-owner to connect and create the first Owner together.");
    }

    // Atomically redeems the WordPress-generated code and creates the first Owner.
    [Function("RegisterFirstOwner")]
    public async Task<HttpResponseData> RegisterOwner(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/register-owner")] HttpRequestData req)
    {
        var body = await req.ReadFromJsonAsync<OwnerRegistrationRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.Code) ||
            string.IsNullOrWhiteSpace(body.DisplayName) || string.IsNullOrWhiteSpace(body.Email) ||
            string.IsNullOrWhiteSpace(body.Password))
            return await Error(req, HttpStatusCode.BadRequest,
                "Connection code, name, email and password are required.");
        if (body.Password.Length < 12)
            return await Error(req, HttpStatusCode.BadRequest,
                "Choose a password with at least 12 characters.");

        var result = await _security.RegisterOwnerAsync(body.Code, body.DisplayName,
            body.Email, body.Password);
        if (result == null)
            return await Error(req, HttpStatusCode.Conflict,
                "The connection code is invalid, already used, or the organisation has already been claimed.");
        var welcomeEmailSent = false;
        try
        {
            await _email.SendOwnerWelcomeAsync(result.Email, result.DisplayName, result.SiteName);
            welcomeEmailSent = true;
        }
        catch (Exception ex)
        {
            // Account creation must not be undone by a temporary email delivery failure.
            Console.Error.WriteLine($"TicketPilot owner welcome email failed: {ex.Message}");
        }

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(new { success = true, result.TenantId,
            result.SiteName, result.SiteUrl, result.UserId, result.DisplayName,
            result.Email, result.Role, result.AccessToken, result.TokenType, result.ExpiresIn,
            welcomeEmailSent });
        return response;
    }

    [Function("UserLogin")]
    public async Task<HttpResponseData> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/login")] HttpRequestData req)
    {
        var body = await req.ReadFromJsonAsync<UserLoginRequest>();
        if (body == null || string.IsNullOrWhiteSpace(body.TenantId) ||
            string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrEmpty(body.Password))
            return await Error(req, HttpStatusCode.BadRequest, "Organisation, email and password are required.");
        var result = await _security.LoginUserAsync(body.TenantId, body.Email, body.Password);
        if (result == null)
            return await Error(req, HttpStatusCode.Unauthorized, "Email or password is incorrect.");
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { success = true, result.TenantId, result.UserId,
            result.DisplayName, result.Email, result.Role, result.AccessToken,
            result.TokenType, result.ExpiresIn });
        return response;
    }

    [Function("UserMe")]
    public async Task<HttpResponseData> Me(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "app/me")] HttpRequestData req)
    {
        var token = ReadBearerToken(req);
        var principal = token == null ? null : await _security.ValidateUserAccessTokenAsync(token);
        if (principal == null) return await Error(req, HttpStatusCode.Unauthorized, "A valid staff login is required.");
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { success = true, principal.TenantId,
            principal.UserId, principal.Email, principal.Role });
        return response;
    }

    [Function("UserLogout")]
    public async Task<HttpResponseData> Logout(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/logout")] HttpRequestData req)
    {
        var token = ReadBearerToken(req);
        if (token == null) return await Error(req, HttpStatusCode.Unauthorized, "A valid staff login is required.");
        var principal = await _security.ValidateUserAccessTokenAsync(token);
        if (principal == null) return await Error(req, HttpStatusCode.Unauthorized, "A valid staff login is required.");
        await _security.RevokeUserAccessTokenAsync(token);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { success = true, message = "You have been signed out." });
        return response;
    }

    [Function("CreateUserInvitation")]
    public async Task<HttpResponseData> InviteUser([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/users/invitations")] HttpRequestData req)
    {
        var actor = await GetPrincipal(req); if (actor == null) return await Error(req, HttpStatusCode.Unauthorized, "A valid staff login is required.");
        var body = await req.ReadFromJsonAsync<CreateInvitationRequest>(); if (body == null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.DisplayName) || string.IsNullOrWhiteSpace(body.Role)) return await Error(req, HttpStatusCode.BadRequest, "Email, display name and role are required.");
        try
        {
            var x = await _security.CreateUserInvitationAsync(actor, body.Email, body.DisplayName, body.Role);
            try
            {
                await _email.SendInvitationAsync(x.Email, x.DisplayName, x.Role, x.InvitationToken);
            }
            catch (Exception ex)
            {
                // Keep the invitation pending so an administrator can retry after email is configured.
                Console.Error.WriteLine($"TicketPilot invitation email failed: {ex.Message}");
                await _security.RevokeUserInvitationAsync(actor, x.InvitationToken);
                return await Error(req, HttpStatusCode.ServiceUnavailable,
                    "The invitation email could not be sent. No invitation was left active. Check the TicketPilot email configuration and try again.");
            }

            var res = req.CreateResponse(HttpStatusCode.Created);
            await res.WriteAsJsonAsync(new
            {
                success = true, x.Email, x.DisplayName, x.Role, x.CreatedAt,
                expiresAt = (DateTimeOffset?)null,
                emailSent = true,
                message = "Invitation email sent. The invitation remains valid until accepted or revoked."
            });
            return res;
        }
        catch (UnauthorizedAccessException ex) { return await Error(req, HttpStatusCode.Forbidden, ex.Message); }
        catch (ArgumentException ex) { return await Error(req, HttpStatusCode.BadRequest, ex.Message); }
        catch (InvalidOperationException ex) { return await Error(req, HttpStatusCode.Conflict, ex.Message); }
    }

    [Function("AcceptUserInvitation")]
    public async Task<HttpResponseData> AcceptInvitation([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/users/invitations/accept")] HttpRequestData req)
    {
        var b = await req.ReadFromJsonAsync<AcceptInvitationRequest>(); if (b == null || string.IsNullOrWhiteSpace(b.InvitationToken) || string.IsNullOrWhiteSpace(b.DisplayName) || string.IsNullOrWhiteSpace(b.Password)) return await Error(req, HttpStatusCode.BadRequest, "Invitation token, name and password are required.");
        if (b.Password.Length < 12) return await Error(req, HttpStatusCode.BadRequest, "Choose a password with at least 12 characters.");
        var x = await _security.AcceptUserInvitationAsync(b.InvitationToken, b.DisplayName, b.Password); if (x == null) return await Error(req, HttpStatusCode.Conflict, "The invitation is invalid, already accepted, or revoked.");
        var res = req.CreateResponse(HttpStatusCode.Created); await res.WriteAsJsonAsync(new { success = true, x.TenantId, x.UserId, x.DisplayName, x.Email, x.Role, x.AccessToken, x.TokenType, x.ExpiresIn }); return res;
    }

    [Function("ListOrganisationUsers")]
    public async Task<HttpResponseData> ListUsers([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "app/users")] HttpRequestData req)
    {
        var actor = await GetPrincipal(req); if (actor == null) return await Error(req, HttpStatusCode.Unauthorized, "A valid staff login is required.");
        if (actor.Role != "OWNER" && actor.Role != "ADMINISTRATOR") return await Error(req, HttpStatusCode.Forbidden, "You cannot manage users.");
        var users = await _security.ListTenantUsersAsync(actor.TenantId); var res = req.CreateResponse(HttpStatusCode.OK); await res.WriteAsJsonAsync(new { success = true, users }); return res;
    }

    [Function("ListPendingInvitations")]
    public async Task<HttpResponseData> ListInvitations([HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "app/users/invitations")] HttpRequestData req)
    {
        var actor = await GetPrincipal(req); if (actor == null) return await Error(req, HttpStatusCode.Unauthorized, "A valid staff login is required.");
        if (actor.Role != "OWNER" && actor.Role != "ADMINISTRATOR") return await Error(req, HttpStatusCode.Forbidden, "You cannot manage invitations.");
        var invitations = await _security.ListTenantInvitationsAsync(actor.TenantId); var res = req.CreateResponse(HttpStatusCode.OK); await res.WriteAsJsonAsync(new { success = true, invitations }); return res;
    }

    [Function("ChangeOrganisationUserRole")]
    public async Task<HttpResponseData> ChangeRole([HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "app/users/{userId}/role")] HttpRequestData req, string userId)
    {
        var actor = await GetPrincipal(req); if (actor == null) return await Error(req, HttpStatusCode.Unauthorized, "A valid staff login is required.");
        var b = await req.ReadFromJsonAsync<ChangeRoleRequest>(); if (b == null || string.IsNullOrWhiteSpace(b.Role)) return await Error(req, HttpStatusCode.BadRequest, "A role is required.");
        if (!await _security.ChangeUserRoleAsync(actor, userId, b.Role)) return await Error(req, HttpStatusCode.Forbidden, "Role change not permitted or user not found.");
        var res = req.CreateResponse(HttpStatusCode.OK); await res.WriteAsJsonAsync(new { success = true }); return res;
    }

    [Function("DeactivateOrganisationUser")]
    public async Task<HttpResponseData> DeactivateUser([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/users/{userId}/deactivate")] HttpRequestData req, string userId)
    {
        var actor = await GetPrincipal(req); if (actor == null) return await Error(req, HttpStatusCode.Unauthorized, "A valid staff login is required.");
        if (!await _security.DeactivateTenantUserAsync(actor, userId)) return await Error(req, HttpStatusCode.Forbidden, "User deactivation not permitted or user not found.");
        var res = req.CreateResponse(HttpStatusCode.OK); await res.WriteAsJsonAsync(new { success = true, message = "User deactivated. Existing sessions are invalidated." }); return res;
    }

    [Function("RevokeUserInvitation")]
    public async Task<HttpResponseData> RevokeInvitation([HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "app/users/invitations/revoke")] HttpRequestData req)
    {
        var actor = await GetPrincipal(req); if (actor == null) return await Error(req, HttpStatusCode.Unauthorized, "A valid staff login is required.");
        var b = await req.ReadFromJsonAsync<RevokeInvitationRequest>(); if (b == null || string.IsNullOrWhiteSpace(b.InvitationToken)) return await Error(req, HttpStatusCode.BadRequest, "Invitation token is required.");
        if (!await _security.RevokeUserInvitationAsync(actor, b.InvitationToken)) return await Error(req, HttpStatusCode.NotFound, "Pending invitation not found.");
        var res = req.CreateResponse(HttpStatusCode.OK); await res.WriteAsJsonAsync(new { success = true }); return res;
    }

    private async Task<UserPrincipal?> GetPrincipal(HttpRequestData req) { var token = ReadBearerToken(req); return token == null ? null : await _security.ValidateUserAccessTokenAsync(token); }

    private static string? ReadBearerToken(HttpRequestData req)
    {
        if (!req.Headers.TryGetValues("Authorization", out var values)) return null;
        var header = values.FirstOrDefault();
        return header != null && header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? header.Substring(7).Trim() : null;
    }

    private static async Task<HttpResponseData> Error(HttpRequestData req, HttpStatusCode status, string message)
    {
        var response = req.CreateResponse(status);
        await response.WriteAsJsonAsync(new { success = false, message });
        return response;
    }
}

public class ActivationKeyRequest { public string? Label { get; set; } }
public class WebsiteActivationRequest { public string? ActivationKey { get; set; } public string? SiteUrl { get; set; } public string? SiteName { get; set; } }
public class OwnerRegistrationRequest { public string? Code { get; set; } public string? DisplayName { get; set; } public string? Email { get; set; } public string? Password { get; set; } }
public class UserLoginRequest { public string? TenantId { get; set; } public string? Email { get; set; } public string? Password { get; set; } }

public class CreateInvitationRequest { public string? Email { get; set; } public string? DisplayName { get; set; } public string? Role { get; set; } }
public class AcceptInvitationRequest { public string? InvitationToken { get; set; } public string? DisplayName { get; set; } public string? Password { get; set; } }
public class ChangeRoleRequest { public string? Role { get; set; } }
public class RevokeInvitationRequest { public string? InvitationToken { get; set; } }
