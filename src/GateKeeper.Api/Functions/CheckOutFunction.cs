using GateKeeper.Shared.DTOs;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class CheckOutFunction
{
    private readonly ITicketRepository _tickets;

    public CheckOutFunction(ITicketRepository tickets)
    {
        _tickets = tickets;
    }

    [Function("CheckOut")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "tickets/checkout"
        )]
        HttpRequestData req)
    {
        // =====================================
        // READ REQUEST
        // =====================================

        var request =
            await req.ReadFromJsonAsync<CheckOutRequest>();

        if (request == null)
        {
            return req.CreateResponse(
                HttpStatusCode.BadRequest
            );
        }


        // =====================================
        // FIND TICKET
        // =====================================

        var ticket =
            await _tickets.GetTicketAsync(
                request.EventId,
                request.TicketNumber
            );


        // =====================================
        // TICKET NOT FOUND
        // =====================================

        if (ticket == null)
        {
            var response =
                req.CreateResponse(
                    HttpStatusCode.NotFound
                );

            await response.WriteAsJsonAsync(
                new CheckOutResponse
                {
                    Status = "NOT_FOUND",
                    Message = "Ticket not found"
                }
            );

            return response;
        }


        // =====================================
        // CANCELLED
        // =====================================

        if (ticket.Cancelled)
        {
            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );

            await response.WriteAsJsonAsync(
                new CheckOutResponse
                {
                    Status = "CANCELLED",
                    Message = "Ticket cancelled",
                    TicketNumber = ticket.RowKey,
                    CustomerName = ticket.CustomerName,
                    TicketType = ticket.TicketType
                }
            );

            return response;
        }


        // =====================================
        // ALREADY CHECKED OUT
        // =====================================

        if (ticket.CheckedOut)
        {
            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );

            await response.WriteAsJsonAsync(
                new CheckOutResponse
                {
                    Status = "ALREADY_CHECKED_OUT",
                    Message = "Ticket already checked out",
                    TicketNumber = ticket.RowKey,
                    CustomerName = ticket.CustomerName,
                    TicketType = ticket.TicketType
                }
            );

            return response;
        }


        // =====================================
        // NOT CHECKED IN
        // =====================================

        if (!ticket.CheckedIn)
        {
            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );

            await response.WriteAsJsonAsync(
                new CheckOutResponse
                {
                    Status = "NOT_CHECKED_IN",
                    Message = "Ticket has not been checked in",
                    TicketNumber = ticket.RowKey,
                    CustomerName = ticket.CustomerName,
                    TicketType = ticket.TicketType
                }
            );

            return response;
        }


        // =====================================
        // CHECK OUT TICKET
        // =====================================

        ticket.CheckedIn = false;
        ticket.CheckedOut = true;

        // Clear previous check-in timestamp
        ticket.CheckedInAt = null;

        // Record checkout time
        ticket.CheckedOutAt = DateTime.UtcNow;

        ticket.Updated = DateTime.UtcNow;


        // =====================================
        // SAVE
        // =====================================

        await _tickets.UpdateTicketAsync(ticket);


        // =====================================
        // SUCCESS
        // =====================================

        var ok =
            req.CreateResponse(
                HttpStatusCode.OK
            );

        await ok.WriteAsJsonAsync(
            new CheckOutResponse
            {
                Status = "SUCCESS",
                Message = "Checked out successfully",
                TicketNumber = ticket.RowKey,
                CustomerName = ticket.CustomerName,
                TicketType = ticket.TicketType
            }
        );

        return ok;
    }
}