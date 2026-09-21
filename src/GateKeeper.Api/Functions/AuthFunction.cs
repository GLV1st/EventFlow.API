using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class AuthFunction
{
    [Function("Auth")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "auth"
        )]
        HttpRequestData req)
    {
        var request =
            await req.ReadFromJsonAsync<AuthRequest>();

        if (request == null ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return await CreateResponse(
                req,
                HttpStatusCode.BadRequest,
                false,
                false,
                "Password is required."
            );
        }


        // =====================================================
        // MAIN GATEKEEPER PASSWORD
        // =====================================================

        var configuredPassword =
            Environment.GetEnvironmentVariable(
                "GateKeeperPassword"
            );


        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            return await CreateResponse(
                req,
                HttpStatusCode.InternalServerError,
                false,
                false,
                "GateKeeperPassword is not configured."
            );
        }


        // =====================================================
        // ADMIN PASSWORD
        // =====================================================

        var configuredAdminPassword =
            Environment.GetEnvironmentVariable(
                "GATEKEEPER_ADMIN_PASSWORD"
            );


        if (string.IsNullOrWhiteSpace(configuredAdminPassword))
        {
            return await CreateResponse(
                req,
                HttpStatusCode.InternalServerError,
                false,
                false,
                "GATEKEEPER_ADMIN_PASSWORD is not configured."
            );
        }


        // =====================================================
        // CHECK MAIN PASSWORD
        // =====================================================

        if (request.Password == configuredPassword)
        {
            return await CreateResponse(
                req,
                HttpStatusCode.OK,
                true,
                false,
                "Access granted."
            );
        }


        // =====================================================
        // CHECK ADMIN PASSWORD
        // =====================================================

        if (request.Password == configuredAdminPassword)
        {
            return await CreateResponse(
                req,
                HttpStatusCode.OK,
                true,
                true,
                "Admin access granted."
            );
        }


        // =====================================================
        // INVALID PASSWORD
        // =====================================================

        return await CreateResponse(
            req,
            HttpStatusCode.Unauthorized,
            false,
            false,
            "Incorrect password."
        );
    }


    private static async Task<HttpResponseData> CreateResponse(
        HttpRequestData req,
        HttpStatusCode status,
        bool authenticated,
        bool admin,
        string message)
    {
        var response =
            req.CreateResponse(status);

        await response.WriteAsJsonAsync(
            new
            {
                Authenticated = authenticated,
                Admin = admin,
                Message = message
            }
        );

        return response;
    }
}


public class AuthRequest
{
    public string? Password { get; set; }
}