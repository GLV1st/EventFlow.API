using EventFlow.Shared.DTOs;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Security.Cryptography;

namespace EventFlow.Api.Functions;

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
        var request =
            await req.ReadFromJsonAsync<GenerateTicketsRequest>();

        if (request == null)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.BadRequest,
                "Invalid request.");
        }

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
        // COP-CARN UNDER 14 SAFETY CHECK
        // =====================================
        //
        // Under 14 tickets use wristbands and
        // therefore MUST NOT receive a EventFlow
        // ticket number or barcode.
        //
        // This is a safety backstop at API level.
        // Even if WooCommerce accidentally sends
        // an Under 14 request, this API will NOT:
        //
        // - create an event
        // - reserve ticket numbers
        // - create TicketEntity records
        // - generate barcodes
        //
        // It simply returns an empty ticket list.
        //

        if (
            request.EventId.Equals(
                "COP-CARN",
                StringComparison.OrdinalIgnoreCase
            )
            &&
            !string.IsNullOrWhiteSpace(request.TicketType)
            &&
            request.TicketType.Contains(
                "Under 14",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            Console.WriteLine(
                "COP-CARN Under 14 ticket request received. " +
                "No EventFlow tickets or ticket numbers will be generated."
            );

            var emptyResponse =
                req.CreateResponse(
                    HttpStatusCode.OK
                );

            await emptyResponse.WriteAsJsonAsync(
                new List<GenerateTicketsResponse>()
            );

            return emptyResponse;
        }

        // =====================================
        // FIND / CREATE EVENT
        // =====================================

        var evt =
            await _events.GetEventAsync(
                request.EventId);

        if (evt == null)
        {
            evt = new EventEntity
            {
                PartitionKey = "EVENT",
                RowKey = request.EventId,
                Name = request.EventName,
                TicketPrefix =
                    request.TicketPrefix
                        .Trim()
                        .ToUpperInvariant(),
                NextTicketNumber = 1
            };

            await _events.AddEventAsync(evt);
        }

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

                    WalletToken =
                        Convert.ToHexString(
                            RandomNumberGenerator.GetBytes(32)
                        ).ToLowerInvariant(),

                    CheckedIn = false,
                    CheckedInAt = null,
                    CheckedOut = false,
                    CheckedOutAt = null,
                    Cancelled = false,
                    FirstScanAt = null,
                    Created = DateTime.UtcNow,
                    Updated = DateTime.UtcNow
                };

            await _tickets.AddTicketAsync(ticket);

            response.Add(
                new GenerateTicketsResponse
                {
                    TicketNumber =
                        ticketNumber,

                    WalletToken =
                        ticket.WalletToken
                });
        }

        // =====================================
        // RETURN RESPONSE
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