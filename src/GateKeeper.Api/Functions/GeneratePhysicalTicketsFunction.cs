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
        // TICKET TYPE
        // =====================================
        //
        // COP Carnival uses:
        //   Under 14 Yrs
        //   Over 14 Yrs
        //
        // All other events have no ticket
        // variation, so TicketType is blank.
        // =====================================

        var isCopCarnival =
            request.EventId.Equals(
                "COP-CARN",
                StringComparison.OrdinalIgnoreCase);

        if (
            isCopCarnival &&
            string.IsNullOrWhiteSpace(request.TicketType)
        )
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "TicketType is required for COP Carnival.");
        }


        // For non-COP events, make sure we store
        // an empty ticket type rather than null.
        if (!isCopCarnival)
        {
            request.TicketType = "";
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
        // RESERVE TICKET NUMBERS
        // =====================================

        int firstTicketNumber;

        try
        {
            firstTicketNumber =
                await _events.ReserveTicketNumbersAsync(
                    request.EventId,
                    request.Quantity);
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "FAILED TO RESERVE PHYSICAL TICKET NUMBERS");

            Console.WriteLine(
                ex.ToString());

            return await ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to reserve ticket numbers.");
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
            // Use the number that was safely reserved
            // for this request.

            var ticketNumber =
                $"{evt.TicketPrefix}-{(firstTicketNumber + i):D6}";


            // =====================================
            // CREATE PHYSICAL TICKET
            // =====================================

            var ticket =
                new TicketEntity
                {
                    PartitionKey =
                        request.EventId,

                    RowKey =
                        ticketNumber,

                    // Physical tickets are not linked
                    // to a WooCommerce order.
                    OrderId = "",

                    TicketType =
                        request.TicketType,

                    EventDate = "",

                    CustomerName = "",

                    CustomerEmail = "",

                    CheckedIn = false,

                    CheckedInAt = null,

                    CheckedOut = false,

                    CheckedOutAt = null,

                    Cancelled = false,

                    // Empty until the ticket is
                    // actually scanned at the event.
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
            // ADD TO RESPONSE
            // =====================================

            response.Add(
                new GenerateTicketsResponse
                {
                    TicketNumber =
                        ticketNumber
                });
        }


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