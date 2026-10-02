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
    private readonly ICateringVendorRepository _cateringVendors;

    public CheckInFunction(
        ITicketRepository tickets,
        ITraderRepository traders,
        ICateringVendorRepository cateringVendors)
    {
        _tickets = tickets;
        _traders = traders;
        _cateringVendors = cateringVendors;
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

                // Trader/food-vendor passes represent a group.
                // Each scan adds ONE person, up to NumberOfPeople.
                var bookedPeople = Math.Max(0, trader.NumberOfPeople);
                var peopleOnSite = Math.Max(0, trader.PeopleOnSite);

                if (bookedPeople <= 0)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new CheckInResponse
                    {
                        Status = "CAPACITY_REACHED",
                        Message = "Trader Pass has no people booked",
                        TicketNumber = trader.TraderPassNumber,
                        CustomerName = trader.ContactName,
                        TicketType = trader.TraderType,
                        PeopleOnSite = peopleOnSite,
                        PeopleBooked = bookedPeople
                    });
                    return response;
                }

                if (peopleOnSite >= bookedPeople)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new CheckInResponse
                    {
                        Status = "CAPACITY_REACHED",
                        Message = $"Maximum reached: {peopleOnSite}/{bookedPeople} people already on site",
                        TicketNumber = trader.TraderPassNumber,
                        CustomerName = trader.ContactName,
                        TicketType = trader.TraderType,
                        PeopleOnSite = peopleOnSite,
                        PeopleBooked = bookedPeople
                    });
                    return response;
                }

                var scanTime = DateTime.UtcNow;

                trader.PeopleOnSite = peopleOnSite + 1;
                trader.CheckedIn = true;
                trader.CheckedOut = false;
                trader.CheckedInAt = scanTime;
                trader.CheckedOutAt = null;
                trader.Updated = scanTime;

                await _traders.UpdateTraderAsync(trader);

                var okTrader = req.CreateResponse(HttpStatusCode.OK);
                await okTrader.WriteAsJsonAsync(new CheckInResponse
                {
                    Status = "SUCCESS",
                    Message = $"Welcome! Trader checked in ({trader.PeopleOnSite}/{bookedPeople})",
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
        // Only use this path if it was not a normal ticket or trader.

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
                    await wrongEvent.WriteAsJsonAsync(new CheckInResponse
                    {
                        Status = "NOT_FOUND",
                        Message = "Catering Pass is not valid for this event"
                    });
                    return wrongEvent;
                }

                // Food-vendor passes use the same COP-TRADER- format.
                // Each scan adds ONE person, up to NumberOfPeople.
                var bookedPeople = Math.Max(0, catering.NumberOfPeople);
                var peopleOnSite = Math.Max(0, catering.PeopleOnSite);

                if (bookedPeople <= 0)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new CheckInResponse
                    {
                        Status = "CAPACITY_REACHED",
                        Message = "Food Vendor Pass has no people booked",
                        TicketNumber = catering.CateringPassNumber,
                        CustomerName = catering.CompanyName,
                        TicketType = "Catering",
                        PeopleOnSite = peopleOnSite,
                        PeopleBooked = bookedPeople
                    });
                    return response;
                }

                if (peopleOnSite >= bookedPeople)
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(new CheckInResponse
                    {
                        Status = "CAPACITY_REACHED",
                        Message = $"Maximum reached: {peopleOnSite}/{bookedPeople} people already on site",
                        TicketNumber = catering.CateringPassNumber,
                        CustomerName = catering.CompanyName,
                        TicketType = "Catering",
                        PeopleOnSite = peopleOnSite,
                        PeopleBooked = bookedPeople
                    });
                    return response;
                }

                var scanTime = DateTime.UtcNow;

                catering.PeopleOnSite = peopleOnSite + 1;
                catering.CheckedIn = true;
                catering.CheckedOut = false;
                catering.CheckedInAt = scanTime;
                catering.CheckedOutAt = null;

                await _cateringVendors.UpdateCateringVendorAsync(catering);

                var okCatering = req.CreateResponse(HttpStatusCode.OK);
                await okCatering.WriteAsJsonAsync(new CheckInResponse
                {
                    Status = "SUCCESS",
                    Message = $"Welcome! Food Vendor checked in ({catering.PeopleOnSite}/{bookedPeople})",
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
