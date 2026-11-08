
using EventFlow.Api.Security;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class AdminAuthFunction
{
    private readonly ITenantSecurityService _security;

    public AdminAuthFunction(ITenantSecurityService security)
    {
        _security = security;
    }

    [Function("GenerateRegistrationCode")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "event-auth")] HttpRequestData req)
    {
        try
        {
            var result =
                await _security.GenerateRegistrationCodeAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new
            {
                Success = true,
                result.Code,
                result.ExpiresAt,
                Message = "Registration code generated successfully."
            });

            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Registration code generation failed: {ex}");

            var response =
                req.CreateResponse(HttpStatusCode.InternalServerError);

            await response.WriteAsJsonAsync(new
            {
                Success = false,
                Message = "Registration code could not be generated."
            });

            return response;
        }
    }
}
