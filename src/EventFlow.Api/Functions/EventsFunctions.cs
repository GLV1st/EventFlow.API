using EventFlow.Shared.DTOs;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class EventsFunction
{
    private readonly IEventRepository _repository;

    public EventsFunction(IEventRepository repository)
    {
        _repository = repository;
    }

    [Function("Events")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "events"
        )]
        HttpRequestData req)
    {
        var events = await _repository.GetEventsAsync();

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