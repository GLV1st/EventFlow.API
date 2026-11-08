
using EventFlow.Api.Security;
using EventFlow.Shared.DTOs;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class EventsFunction
{
    private readonly IEventRepository _repository;
    private readonly ITenantSecurityService _tenantSecurity;

    public EventsFunction(
        IEventRepository repository,
        ITenantSecurityService tenantSecurity)
    {
        _repository = repository;
        _tenantSecurity = tenantSecurity;
    }

    [Function("Events")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "events")]
        HttpRequestData req)
    {
        // Validate bearer token.
        if (!req.Headers.TryGetValues("Authorization", out var values))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var authHeader = values.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var accessToken = authHeader["Bearer ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var tenantId =
            await _tenantSecurity.ValidateAccessTokenAsync(accessToken);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        // Retrieve events belonging only to this tenant.
        var events = await _repository.GetEventsAsync(tenantId);

        var result = events.Select(e => new EventResponse
        {
            Id = e.RowKey,
            Name = e.Name
        }).ToList();

        var response = req.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(result);

        return response;
    }
}
