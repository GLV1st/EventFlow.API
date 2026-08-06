using GateKeeper.Shared.DTOs;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class CheckInFunction
{
    private readonly ITicketRepository _tickets;

    public CheckInFunction(ITicketRepository tickets)
    {
        _tickets = tickets;
    }

    [Function("CheckIn")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tickets/checkin")]
        HttpRequestData req)
    {
        var request = await req.ReadFromJsonAsync<CheckInRequest>();

        if (request == null)
            return req.CreateResponse(HttpStatusCode.BadRequest);

        var ticket = await _tickets.GetTicketAsync(
            request.EventId,
            request.TicketNumber);

        if (ticket == null)
        {
            var response = req.CreateResponse(HttpStatusCode.NotFound);

            await response.WriteAsJsonAsync(new CheckInResponse
            {
                Status = "NOT_FOUND",
                Message = "Ticket not found"
            });

            return response;
        }

        if (ticket.Cancelled)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new CheckInResponse
            {
                Status = "CANCELLED",
                Message = "Ticket cancelled"
            });

            return response;
        }

        if (ticket.CheckedIn)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new CheckInResponse
            {
                Status = "ALREADY_CHECKED_IN",
                Message = "Ticket already checked in",
                TicketNumber = ticket.RowKey,
                CustomerName = ticket.CustomerName,
                TicketType = ticket.TicketType
            });

            return response;
        }

        ticket.CheckedIn = true;
        ticket.CheckedInAt = DateTime.UtcNow;
        ticket.Updated = DateTime.UtcNow;

        await _tickets.UpdateTicketAsync(ticket);

        var ok = req.CreateResponse(HttpStatusCode.OK);

        await ok.WriteAsJsonAsync(new CheckInResponse
        {
            Status = "SUCCESS",
            Message = "Welcome!",
            TicketNumber = ticket.RowKey,
            CustomerName = ticket.CustomerName,
            TicketType = ticket.TicketType
        });

        return ok;
    }
}