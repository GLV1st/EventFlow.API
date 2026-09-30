using GateKeeper.Shared.DTOs;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class CheckOutFunction
{
    private readonly ITicketRepository _tickets;
    private readonly ITraderRepository _traders;
    private readonly ICateringVendorRepository _cateringVendors;

    public CheckOutFunction(
        ITicketRepository tickets,
        ITraderRepository traders,
        ICateringVendorRepository cateringVendors)
    {
        _tickets = tickets;
        _traders = traders;
        _cateringVendors = cateringVendors;
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
        // FIND NORMAL TICKET
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
                        new CheckOutResponse
                        {
                            Status = "NOT_FOUND",
                            Message = "Trader Pass is not valid for this event"
                        }
                    );

                    return wrongEvent;
                }

                // Already checked out.
                if (trader.CheckedOut)
                {
                    var response =
                        req.CreateResponse(
                            HttpStatusCode.OK
                        );

                    await response.WriteAsJsonAsync(
                        new CheckOutResponse
                        {
                            Status = "ALREADY_CHECKED_OUT",
                            Message = "Trader already checked out",
                            TicketNumber = trader.TraderPassNumber,
                            CustomerName = trader.ContactName,
                            TicketType = trader.TraderType
                        }
                    );

                    return response;
                }

                // Must have checked in first.
                if (!trader.CheckedIn)
                {
                    var response =
                        req.CreateResponse(
                            HttpStatusCode.OK
                        );

                    await response.WriteAsJsonAsync(
                        new CheckOutResponse
                        {
                            Status = "NOT_CHECKED_IN",
                            Message = "Trader has not been checked in",
                            TicketNumber = trader.TraderPassNumber,
                            CustomerName = trader.ContactName,
                            TicketType = trader.TraderType
                        }
                    );

                    return response;
                }

                // Check out trader.
                var checkoutTime = DateTime.UtcNow;

                trader.CheckedIn = false;
                trader.CheckedOut = true;
                trader.CheckedInAt = null;
                trader.CheckedOutAt = checkoutTime;
                trader.Updated = checkoutTime;

                await _traders.UpdateTraderAsync(trader);

                var okTrader =
                    req.CreateResponse(
                        HttpStatusCode.OK
                    );

                await okTrader.WriteAsJsonAsync(
                    new CheckOutResponse
                    {
                        Status = "SUCCESS",
                        Message = "Trader checked out successfully",
                        TicketNumber = trader.TraderPassNumber,
                        CustomerName = trader.ContactName,
                        TicketType = trader.TraderType
                    }
                );

                return okTrader;
            }
        }

        // =====================================
        // CATERING PASS FALLBACK
        // =====================================
        // Catering passes use the same COP-TRADER- number format
        // as Trader passes, but are stored in CateringVendors.

        if (ticket == null &&
            request.TicketNumber.StartsWith(
                "COP-TRADER-",
                StringComparison.OrdinalIgnoreCase))
        {
            var catering =
                await _cateringVendors.GetCateringVendorAsync(
                    request.EventId,
                    request.TicketNumber
                );

            if (catering != null)
            {
                if (!string.Equals(
                        catering.EventId,
                        request.EventId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    var wrongEvent = req.CreateResponse(HttpStatusCode.NotFound);
                    await wrongEvent.WriteAsJsonAsync(new CheckOutResponse
                    {
                        Status = "NOT_FOUND",
                        Message = "Catering Pass is not valid for this event"
                    });
                    return wrongEvent;
                }

                if (catering.CheckedOut)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new CheckOutResponse
                    {
                        Status = "ALREADY_CHECKED_OUT",
                        Message = "Catering Pass already checked out",
                        TicketNumber = catering.CateringPassNumber,
                        CustomerName = catering.CompanyName,
                        TicketType = "Catering"
                    });
                    return response;
                }

                if (!catering.CheckedIn)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new CheckOutResponse
                    {
                        Status = "NOT_CHECKED_IN",
                        Message = "Catering Pass has not been checked in",
                        TicketNumber = catering.CateringPassNumber,
                        CustomerName = catering.CompanyName,
                        TicketType = "Catering"
                    });
                    return response;
                }

                var checkoutTime = DateTime.UtcNow;

                catering.CheckedIn = false;
                catering.CheckedOut = true;
                catering.CheckedInAt = null;
                catering.CheckedOutAt = checkoutTime;

                await _cateringVendors.UpdateCateringVendorAsync(catering);

                var okCatering = req.CreateResponse(HttpStatusCode.OK);
                await okCatering.WriteAsJsonAsync(new CheckOutResponse
                {
                    Status = "SUCCESS",
                    Message = "Catering Pass checked out successfully",
                    TicketNumber = catering.CateringPassNumber,
                    CustomerName = catering.CompanyName,
                    TicketType = "Catering"
                });
                return okCatering;
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

        var ticketCheckoutTime = DateTime.UtcNow;

        ticket.CheckedIn = false;
        ticket.CheckedOut = true;
        ticket.CheckedInAt = null;
        ticket.CheckedOutAt = ticketCheckoutTime;
        ticket.Updated = ticketCheckoutTime;

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
