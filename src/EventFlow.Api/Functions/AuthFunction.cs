
using EventFlow.Api.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class AuthFunction
{
    private readonly ITenantSecurityService _security;

    public AuthFunction(ITenantSecurityService security)
    {
        _security = security;
    }

    // =========================================
    // REGISTER WEBSITE
    // =========================================

    [Function("RegisterTenant")]
    public async Task<HttpResponseData> Register(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "auth/register")] HttpRequestData req)
    {
        try
        {
            var request =
                await req.ReadFromJsonAsync<TenantRegistrationRequest>();

            if (request == null ||
                string.IsNullOrWhiteSpace(request.Code) ||
                string.IsNullOrWhiteSpace(request.SiteUrl) ||
                string.IsNullOrWhiteSpace(request.SiteName))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "Registration code, website URL and website name are required.");
            }

            var result =
                await _security.RedeemRegistrationCodeAsync(
                    request.Code,
                    request.SiteUrl,
                    request.SiteName);

            if (result == null)
            {
                return await Error(
                    req,
                    HttpStatusCode.Unauthorized,
                    "Registration code is invalid, expired or already used.");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new
            {
                Success = true,
                result.TenantId,
                result.ClientId,
                result.ClientSecret,
                Message = "Website successfully registered."
            });

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Tenant registration failed: {ex}");

            return await Error(
                req,
                HttpStatusCode.InternalServerError,
                "Registration could not be completed.");
        }
    }

    // =========================================
    // EXCHANGE CLIENT CREDENTIALS FOR TOKEN
    // =========================================

    [Function("CreateAccessToken")]
    public async Task<HttpResponseData> Token(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "auth/token")] HttpRequestData req)
    {
        try
        {
            var request =
                await req.ReadFromJsonAsync<TokenRequest>();

            if (request == null ||
                string.IsNullOrWhiteSpace(request.ClientId) ||
                string.IsNullOrWhiteSpace(request.ClientSecret))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "Client ID and Client Secret are required.");
            }

            var result =
                await _security.CreateAccessTokenAsync(
                    request.ClientId,
                    request.ClientSecret);

            if (result == null)
            {
                return await Error(
                    req,
                    HttpStatusCode.Unauthorized,
                    "Invalid client credentials.");
            }

            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new
            {
                result.AccessToken,
                result.TokenType,
                result.ExpiresIn
            });

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token creation failed: {ex}");

            return await Error(
                req,
                HttpStatusCode.InternalServerError,
                "Access token could not be created.");
        }
    }

    // =========================================
    // ERROR RESPONSE
    // =========================================

    private static async Task<HttpResponseData> Error(
        HttpRequestData req,
        HttpStatusCode status,
        string message)
    {
        var response = req.CreateResponse(status);

        await response.WriteAsJsonAsync(new
        {
            Success = false,
            Message = message
        });

        return response;
    }
}

// =========================================
// REQUEST MODELS
// =========================================

public class TenantRegistrationRequest
{
    public string? Code { get; set; }
    public string? SiteUrl { get; set; }
    public string? SiteName { get; set; }
}

public class TokenRequest
{
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
}
