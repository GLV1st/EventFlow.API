using GateKeeper.Shared.DTOs;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class CheckInFunction
{
    private readonly ITicketRepository _tickets;
    private readonly ITraderRepository _traders;

    public CheckInFunction(
        ITicketRepository tickets,
        ITraderRepository traders)
    {
        _tickets = tickets;
        _traders = traders;
    }

    [Function("CheckIn")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "tickets/checkin"
        )]
        HttpRequestData req)
    {
        // =====================================
        // READ REQUEST
        // =====================================

        var request =
            await req.ReadFromJsonAsync<CheckInRequest>();

        if (request == null)
        {
            return req.CreateResponse(
                HttpStatusCode.BadRequest
            );
        }

        // =====================================
        // GET NORMAL TICKET
        // =====================================

        var ticket =
            await _tickets.GetTicketAsync(
                request.EventId,
                request.TicketNumber
            );

        // =====================================
        // TRADER PASS FALLBACK
        // =====================================
        //
        // Normal tickets always use the existing Tickets table.
        // Only if no normal ticket is found AND the scanned value
        // looks like a Trader Pass do we look in the Traders table.
        //
        // This keeps the existing ticket check-in path unchanged.

        if (ticket == null &&
            request.TicketNumber.StartsWith(
                "COP-TRADER-",
                StringComparison.OrdinalIgnoreCase))
        {
            var trader =
                await _traders.GetTraderAsync(
                    request.EventId,
                    request.TicketNumber
                );

            if (trader != null)
            {
                // Explicit event safety check.
                if (!string.Equals(
                        trader.EventId,
                        request.EventId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var wrongEvent =
                        req.CreateResponse(
                            HttpStatusCode.NotFound
                        );

                    await wrongEvent.WriteAsJsonAsync(
                        new CheckInResponse
                        {
                            Status = "NOT_FOUND",
                            Message = "Trader Pass is not valid for this event"
                        }
                    );

                    return wrongEvent;
                }

                // Already checked in.
                if (trader.CheckedIn)
                {
                    var response =
                        req.CreateResponse(
                            HttpStatusCode.OK
                        );

                    await response.WriteAsJsonAsync(
                        new CheckInResponse
                        {
                            Status = "ALREADY_CHECKED_IN",
                            Message = "Trader already checked in",
                            TicketNumber = trader.TraderPassNumber,
                            CustomerName = trader.ContactName,
                            TicketType = trader.TraderType
                        }
                    );

                    return response;
                }

                // Check in trader.
                var scanTime = DateTime.UtcNow;

                trader.CheckedIn = true;
                trader.CheckedOut = false;
                trader.CheckedInAt = scanTime;
                trader.CheckedOutAt = null;
                trader.Updated = scanTime;

                await _traders.UpdateTraderAsync(trader);

                var okTrader =
                    req.CreateResponse(
                        HttpStatusCode.OK
                    );

                await okTrader.WriteAsJsonAsync(
                    new CheckInResponse
                    {
                        Status = "SUCCESS",
                        Message = "Welcome! Trader checked in",
                        TicketNumber = trader.TraderPassNumber,
                        CustomerName = trader.ContactName,
                        TicketType = trader.TraderType
                    }
                );

                return okTrader;
            }
        }

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
                new CheckInResponse
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
                new CheckInResponse
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
        // ALREADY CHECKED IN
        // =====================================

        if (ticket.CheckedIn)
        {
            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );

            await response.WriteAsJsonAsync(
                new CheckInResponse
                {
                    Status = "ALREADY_CHECKED_IN",
                    Message = "Ticket already checked in",
                    TicketNumber = ticket.RowKey,
                    CustomerName = ticket.CustomerName,
                    TicketType = ticket.TicketType
                }
            );

            return response;
        }

        // =====================================
        // CHECK IN
        // =====================================

        var ticketScanTime = DateTime.UtcNow;

        ticket.CheckedIn = true;
        ticket.CheckedOut = false;
        ticket.CheckedInAt = ticketScanTime;
        ticket.CheckedOutAt = null;

        // FirstScanAt is permanent.
        if (ticket.FirstScanAt == null)
        {
            ticket.FirstScanAt = ticketScanTime;
        }

        ticket.Updated = ticketScanTime;

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
            new CheckInResponse
            {
                Status = "SUCCESS",
                Message = "Welcome!",
                TicketNumber = ticket.RowKey,
                CustomerName = ticket.CustomerName,
                TicketType = ticket.TicketType
            }
        );

        return ok;
    }
}
