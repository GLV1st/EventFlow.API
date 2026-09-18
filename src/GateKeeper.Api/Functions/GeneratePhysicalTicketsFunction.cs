using GateKeeper.Shared.DTOs;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class GeneratePhysicalTicketsFunction
{
    private readonly IEventRepository _events;
    private readonly ITicketRepository _tickets;

    public GeneratePhysicalTicketsFunction(
        IEventRepository events,
        ITicketRepository tickets)
    {
        _events = events;
        _tickets = tickets;
    }


    [Function("GeneratePhysicalTickets")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "tickets/generate-physical"
        )]
        HttpRequestData req)
    {
        // =====================================
        // READ REQUEST
        // =====================================

        var request =
            await req.ReadFromJsonAsync<
                GeneratePhysicalTicketsRequest>();

        if (request == null)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "Invalid request.");
        }


        // =====================================
        // VALIDATE EVENT
        // =====================================

        if (string.IsNullOrWhiteSpace(request.EventId))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "EventId is required.");
        }


        // =====================================
        // VALIDATE TICKET TYPE
        // =====================================

        if (string.IsNullOrWhiteSpace(request.TicketType))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "TicketType is required.");
        }


        // =====================================
        // VALIDATE QUANTITY
        // =====================================

        if (request.Quantity <= 0)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "Quantity must be greater than zero.");
        }


        // =====================================
        // FIND EVENT
        // =====================================

        var evt =
            await _events.GetEventAsync(
                request.EventId);


        if (evt == null)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.NotFound,
                "Event not found.");
        }


        // =====================================
        // CHECK TICKET PREFIX
        // =====================================

        if (string.IsNullOrWhiteSpace(
            evt.TicketPrefix))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "Event has no ticket prefix.");
        }


        // =====================================
        // GENERATE PHYSICAL TICKETS
        // =====================================

        var response =
            new List<GenerateTicketsResponse>();


        for (
            int i = 0;
            i < request.Quantity;
            i++)
        {
            // Use the existing event ticket prefix
            // and the existing next ticket number.

            var ticketNumber =
                $"{evt.TicketPrefix}-{evt.NextTicketNumber:D6}";


            // =====================================
            // CREATE PHYSICAL TICKET
            // =====================================

            var ticket =
                new TicketEntity
                {
                    // The EventId is the partition.
                    PartitionKey =
                        request.EventId,

                    // The physical ticket number is
                    // the unique row key.
                    RowKey =
                        ticketNumber,

                    // Physical tickets are not linked
                    // to a WooCommerce order.
                    OrderId = "",

                    TicketType =
                        request.TicketType,

                    // EventEntity does not currently
                    // contain an EventDate.
                    EventDate = "",

                    // No customer is known when the
                    // physical ticket is generated.
                    CustomerName = "",

                    CustomerEmail = "",

                    // Physical ticket has not yet
                    // been used.
                    CheckedIn = false,

                    CheckedInAt = null,

                    CheckedOut = false,

                    CheckedOutAt = null,

                    Cancelled = false,

                    // This will only be populated when
                    // the ticket is actually scanned.
                    FirstScanAt = null,

                    Created =
                        DateTime.UtcNow,

                    Updated =
                        DateTime.UtcNow
                };


            // =====================================
            // SAVE TICKET
            // =====================================

            await _tickets.AddTicketAsync(
                ticket);


            // =====================================
            // ADD TICKET TO RESPONSE
            // =====================================

            response.Add(
                new GenerateTicketsResponse
                {
                    TicketNumber =
                        ticketNumber
                });


            // =====================================
            // MOVE TO NEXT NUMBER
            // =====================================

            evt.NextTicketNumber++;
        }


        // =====================================
        // SAVE NEXT AVAILABLE NUMBER
        // =====================================

        await _events.UpdateEventAsync(
            evt);


        // =====================================
        // SUCCESS RESPONSE
        // =====================================

        var http =
            req.CreateResponse(
                HttpStatusCode.OK);


        await http.WriteAsJsonAsync(
            response);


        return http;
    }


    // =====================================
    // ERROR RESPONSE
    // =====================================

    private static async Task<HttpResponseData>
        ErrorResponse(
            HttpRequestData req,
            HttpStatusCode status,
            string message)
    {
        var response =
            req.CreateResponse(status);


        await response.WriteAsJsonAsync(
            new
            {
                Status = "ERROR",
                Message = message
            });


        return response;
    }
}