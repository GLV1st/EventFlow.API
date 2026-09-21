using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class AdminAuthFunction
{
    [Function("AdminAuth")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "event-auth"
        )]
        HttpRequestData req)
    {
        var request =
            await req.ReadFromJsonAsync<AdminAuthRequest>();

        if (request == null ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return await CreateResponse(
                req,
                HttpStatusCode.BadRequest,
                false,
                "Password is required."
            );
        }


        // ============================================
        // ADMIN PASSWORD FROM AZURE
        // ============================================

        var configuredPassword =
            Environment.GetEnvironmentVariable(
                "GATEKEEPER_ADMIN_PASSWORD"
            );


        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            return await CreateResponse(
                req,
                HttpStatusCode.InternalServerError,
                false,
                "GATEKEEPER_ADMIN_PASSWORD is not configured."
            );
        }


        // ============================================
        // CHECK PASSWORD
        // ============================================

        if (request.Password != configuredPassword)
        {
            return await CreateResponse(
                req,
                HttpStatusCode.Unauthorized,
                false,
                "Incorrect admin password."
            );
        }


        // ============================================
        // SUCCESS
        // ============================================

        return await CreateResponse(
            req,
            HttpStatusCode.OK,
            true,
            "Admin access granted."
        );
    }


    private static async Task<HttpResponseData> CreateResponse(
        HttpRequestData req,
        HttpStatusCode status,
        bool authenticated,
        string message)
    {
        var response =
            req.CreateResponse(status);

        await response.WriteAsJsonAsync(
            new
            {
                Authenticated = authenticated,
                Message = message
            }
        );

        return response;
    }
}


public class AdminAuthRequest
{
    public string? Password { get; set; }
}