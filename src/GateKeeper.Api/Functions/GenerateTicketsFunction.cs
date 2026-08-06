using GateKeeper.Shared.DTOs;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class GenerateTicketsFunction
{
    private readonly IEventRepository _events;
    private readonly ITicketRepository _tickets;

    public GenerateTicketsFunction(
        IEventRepository events,
        ITicketRepository tickets)
    {
        _events = events;
        _tickets = tickets;
    }

    [Function("GenerateTickets")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tickets/generate")]
        HttpRequestData req)
    {
        var request =
            await req.ReadFromJsonAsync<GenerateTicketsRequest>();

        if (request == null)
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var evt = await _events.GetEventAsync(request.EventId);

        if (evt == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var response = new List<GenerateTicketsResponse>();

        for (int i = 0; i < request.Quantity; i++)
        {
            var ticketNumber =
                $"{evt.TicketPrefix}-{evt.NextTicketNumber:D6}";

            var ticket = new TicketEntity
            {
                PartitionKey = request.EventId,
                RowKey = ticketNumber,

                OrderId = request.OrderId,
                TicketType = request.TicketType,

                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,

                CheckedIn = false,
                CheckedOut = false,
                Cancelled = false,

                Created = DateTime.UtcNow,
                Updated = DateTime.UtcNow
            };

            await _tickets.AddTicketAsync(ticket);

            response.Add(new GenerateTicketsResponse
            {
                TicketNumber = ticketNumber
            });

            evt.NextTicketNumber++;
        }

        await _events.UpdateEventAsync(evt);

        var http = req.CreateResponse(HttpStatusCode.OK);

        await http.WriteAsJsonAsync(response);

        return http;
    }
}