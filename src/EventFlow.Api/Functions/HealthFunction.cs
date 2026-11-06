using EventFlow.Core.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace EventFlow.Api.Functions;

public class HealthFunction
{
    private readonly ILogger<HealthFunction> _logger;
    private readonly IHealthService _healthService;

    public HealthFunction(
        ILogger<HealthFunction> logger,
        IHealthService healthService)
    {
        _logger = logger;
        _healthService = healthService;
    }

    [Function("Health")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "health")] HttpRequestData req)
    {
        _logger.LogInformation("Health endpoint called.");

        var response = req.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(_healthService.GetHealth());

        return response;
    }
}