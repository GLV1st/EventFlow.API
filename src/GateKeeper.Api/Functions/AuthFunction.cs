using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

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
                "Password is required."
            );
        }

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
                "GateKeeperPassword is not configured."
            );
        }

        if (request.Password != configuredPassword)
        {
            return await CreateResponse(
                req,
                HttpStatusCode.Unauthorized,
                false,
                "Incorrect password."
            );
        }

        return await CreateResponse(
            req,
            HttpStatusCode.OK,
            true,
            "Access granted."
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


public class AuthRequest
{
    public string? Password { get; set; }
}