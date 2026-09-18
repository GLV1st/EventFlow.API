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
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "tickets/generate"
        )]
        HttpRequestData req)
    {
        // =====================================
        // READ REQUEST
        // =====================================

        var request =
            await req.ReadFromJsonAsync<GenerateTicketsRequest>();

        if (request == null)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "Invalid request.");
        }


        // =====================================
        // VALIDATE
        // =====================================

        if (string.IsNullOrWhiteSpace(request.EventId))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "EventId is required.");
        }


        if (string.IsNullOrWhiteSpace(request.EventName))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "EventName is required.");
        }


        if (string.IsNullOrWhiteSpace(request.TicketPrefix))
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "TicketPrefix is required.");
        }


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


        // =====================================
        // CREATE EVENT IF IT DOESN'T EXIST
        // =====================================

        if (evt == null)
        {
            evt = new EventEntity
            {
                PartitionKey = "EVENT",

                RowKey =
                    request.EventId,

                Name =
                    request.EventName,

                TicketPrefix =
                    request.TicketPrefix
                        .Trim()
                        .ToUpperInvariant(),

                NextTicketNumber = 1
            };


            await _events.AddEventAsync(evt);
        }


        // =====================================
        // SAFETY CHECK
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
                "FAILED TO RESERVE TICKET NUMBERS"
            );

            Console.WriteLine(
                ex.ToString()
            );

            return await ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to reserve ticket numbers.");
        }


        // =====================================
        // GENERATE TICKETS
        // =====================================

        var response =
            new List<GenerateTicketsResponse>();


        for (
            int i = 0;
            i < request.Quantity;
            i++)
        {
            var ticketNumber =
                $"{evt.TicketPrefix}-{(firstTicketNumber + i):D6}";


            // =====================================
            // CREATE TICKET
            // =====================================

            var ticket =
                new TicketEntity
                {
                    PartitionKey =
                        request.EventId,

                    RowKey =
                        ticketNumber,

                    OrderId =
                        request.OrderId,

                    TicketType =
                        request.TicketType,

                    EventDate =
                        request.EventDate,

                    CustomerName =
                        request.CustomerName,

                    CustomerEmail =
                        request.CustomerEmail,

                    CheckedIn = false,

                    CheckedInAt = null,

                    CheckedOut = false,

                    CheckedOutAt = null,

                    Cancelled = false,

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


            response.Add(
                new GenerateTicketsResponse
                {
                    TicketNumber =
                        ticketNumber
                });
        }


        // =====================================
        // RESPONSE
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