using EventFlow.Shared.DTOs;
using EventFlow.Storage.Interfaces;
using EventFlow.Storage.Entities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class CheckOutFunction
{
    private readonly ITicketRepository _tickets;
    private readonly ITraderRepository _traders;
    private readonly ICateringVendorRepository _cateringVendors;
    private readonly ITicketScanLogRepository _scanLog;

    public CheckOutFunction(
        ITicketRepository tickets,
        ITraderRepository traders,
        ICateringVendorRepository cateringVendors,
        ITicketScanLogRepository scanLog)
    {
        _tickets = tickets;
        _traders = traders;
        _cateringVendors = cateringVendors;
        _scanLog = scanLog;
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

                // Trader/food-vendor passes check out ONE person at a time.
                var bookedPeople = Math.Max(0, trader.NumberOfPeople);
                var peopleOnSite = Math.Max(0, trader.PeopleOnSite);

                if (peopleOnSite <= 0)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new CheckOutResponse
                    {
                        Status = "NOT_CHECKED_IN",
                        Message = "No trader personnel are currently on site",
                        TicketNumber = trader.TraderPassNumber,
                        CustomerName = trader.ContactName,
                        TicketType = trader.TraderType,
                        PeopleOnSite = 0,
                        PeopleBooked = bookedPeople
                    });
                    return response;
                }

                var checkoutTime = DateTime.UtcNow;

                trader.PeopleOnSite = peopleOnSite - 1;
                trader.CheckedIn = trader.PeopleOnSite > 0;
                trader.CheckedOut = trader.PeopleOnSite == 0;
                trader.CheckedOutAt = trader.PeopleOnSite == 0 ? checkoutTime : null;
                trader.Updated = checkoutTime;

                await _traders.UpdateTraderAsync(trader);

                await SaveScanLogAsync(
                    request.EventId,
                    checkoutTime,
                    "OUT",
                    trader.TraderType);

                var okTrader = req.CreateResponse(HttpStatusCode.OK);
                await okTrader.WriteAsJsonAsync(new CheckOutResponse
                {
                    Status = "SUCCESS",
                    Message = $"Trader checked out ({trader.PeopleOnSite}/{bookedPeople} people remain on site)",
                    TicketNumber = trader.TraderPassNumber,
                    CustomerName = trader.ContactName,
                    TicketType = trader.TraderType,
                    PeopleOnSite = trader.PeopleOnSite,
                    PeopleBooked = bookedPeople
                });
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

                var bookedPeople = Math.Max(0, catering.NumberOfPeople);
                var peopleOnSite = Math.Max(0, catering.PeopleOnSite);

                if (peopleOnSite <= 0)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new CheckOutResponse
                    {
                        Status = "NOT_CHECKED_IN",
                        Message = "No food-vendor personnel are currently on site",
                        TicketNumber = catering.CateringPassNumber,
                        CustomerName = catering.CompanyName,
                        TicketType = "Catering",
                        PeopleOnSite = 0,
                        PeopleBooked = bookedPeople
                    });
                    return response;
                }

                var checkoutTime = DateTime.UtcNow;

                catering.PeopleOnSite = peopleOnSite - 1;
                catering.CheckedIn = catering.PeopleOnSite > 0;
                catering.CheckedOut = catering.PeopleOnSite == 0;
                catering.CheckedOutAt = catering.PeopleOnSite == 0 ? checkoutTime : null;

                await _cateringVendors.UpdateCateringVendorAsync(catering);

                await SaveScanLogAsync(
                    request.EventId,
                    checkoutTime,
                    "OUT",
                    "Catering");

                var okCatering = req.CreateResponse(HttpStatusCode.OK);
                await okCatering.WriteAsJsonAsync(new CheckOutResponse
                {
                    Status = "SUCCESS",
                    Message = $"Food Vendor checked out ({catering.PeopleOnSite}/{bookedPeople} people remain on site)",
                    TicketNumber = catering.CateringPassNumber,
                    CustomerName = catering.CompanyName,
                    TicketType = "Catering",
                    PeopleOnSite = catering.PeopleOnSite,
                    PeopleBooked = bookedPeople
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

        await SaveScanLogAsync(
            request.EventId,
            ticketCheckoutTime,
            "OUT",
            ticket.TicketType);

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

    private async Task SaveScanLogAsync(
        string eventId,
        DateTime scanTime,
        string scanType,
        string ticketType)
    {
        try
        {
            await _scanLog.AddScanAsync(new TicketScanLogEntity
            {
                PartitionKey = eventId,
                RowKey = Guid.NewGuid().ToString("N"),
                ScanTime = scanTime,
                ScanType = scanType,
                TicketType = ticketType
            });
        }
        catch (Exception ex)
        {
            // Do not turn a successful ticket scan into a failed scan
            // just because the reporting log could not be written.
            // The failure is logged for investigation.
            Console.WriteLine(
                $"SCAN LOG ERROR: {ex.Message}");
        }
    }
}
