using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class EventStatsFunction
{
    private readonly ITicketRepository _tickets;
    private readonly ITraderRepository _traders;
    private readonly ICateringVendorRepository _cateringVendors;

    public EventStatsFunction(
        ITicketRepository tickets,
        ITraderRepository traders,
        ICateringVendorRepository cateringVendors)
    {
        _tickets = tickets;
        _traders = traders;
        _cateringVendors = cateringVendors;
    }

    [Function("EventStats")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "events/{eventId}/stats"
        )]
        HttpRequestData req,
        string eventId)
    {
        // =====================================
        // VALIDATE EVENT
        // =====================================

        if (string.IsNullOrWhiteSpace(eventId))
        {
            return req.CreateResponse(
                HttpStatusCode.BadRequest
            );
        }

        // =====================================
        // GET NORMAL TICKETS
        // =====================================

        var tickets =
            await _tickets.GetTicketsByEventAsync(
                eventId
            );

        // =====================================
        // GET TRADER PASSES
        // =====================================

        var traders =
            await _traders.GetTradersByEventAsync(
                eventId
            );

        // =====================================
        // GET CATERING PASSES
        // =====================================

        var cateringVendors =
            await _cateringVendors.GetCateringVendorsByEventAsync(
                eventId
            );

        // =====================================
        // NORMAL TICKET STATISTICS
        // =====================================

        var normalTickets =
            tickets.Count;

        var normalCheckedIn =
            tickets.Count(t =>
                t.CheckedIn &&
                !t.Cancelled
            );

        var normalCheckedOut =
            tickets.Count(t =>
                t.CheckedOut &&
                !t.Cancelled
            );

        var normalOnSite =
            tickets.Count(t =>
                t.CheckedIn &&
                !t.CheckedOut &&
                !t.Cancelled
            );

        var normalNotYetIn =
            tickets.Count(t =>
                !t.CheckedIn &&
                !t.CheckedOut &&
                !t.Cancelled
            );

        var cancelled =
            tickets.Count(t =>
                t.Cancelled
            );

        // =====================================
        // NORMAL TICKET TYPE BREAKDOWN
        // =====================================

        var onSiteUnder14 =
            tickets.Count(t =>
                t.CheckedIn &&
                !t.CheckedOut &&
                !t.Cancelled &&
                string.Equals(
                    t.TicketType?.Trim(),
                    "Under 14 Yrs",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        var onSiteOver14 =
            tickets.Count(t =>
                t.CheckedIn &&
                !t.CheckedOut &&
                !t.Cancelled &&
                string.Equals(
                    t.TicketType?.Trim(),
                    "Over 14 Yrs",
                    StringComparison.OrdinalIgnoreCase
                )
            );

        // =====================================
        // TRADER STATISTICS
        // =====================================

        var traderPasses =
            traders.Count;

        var traderPeopleExpected =
            traders.Sum(t =>
                Math.Max(0, t.NumberOfPeople)
            );

        var traderPeopleCheckedIn =
            traders
                .Where(t =>
                    t.CheckedIn &&
                    !t.CheckedOut
                )
                .Sum(t =>
                    Math.Max(0, t.NumberOfPeople)
                );

        var traderPeopleCheckedOut =
            traders
                .Where(t =>
                    t.CheckedOut
                )
                .Sum(t =>
                    Math.Max(0, t.NumberOfPeople)
                );

        var traderPeopleNotYetIn =
            traders
                .Where(t =>
                    !t.CheckedIn &&
                    !t.CheckedOut
                )
                .Sum(t =>
                    Math.Max(0, t.NumberOfPeople)
                );

        // =====================================
        // CATERING STATISTICS
        // =====================================

        var cateringPasses =
            cateringVendors.Count;

        var cateringPeopleExpected =
            cateringVendors.Sum(v =>
                Math.Max(0, v.NumberOfPeople)
            );

        var cateringPeopleCheckedIn =
            cateringVendors
                .Where(v =>
                    v.CheckedIn &&
                    !v.CheckedOut
                )
                .Sum(v =>
                    Math.Max(0, v.NumberOfPeople)
                );

        var cateringPeopleCheckedOut =
            cateringVendors
                .Where(v =>
                    v.CheckedOut
                )
                .Sum(v =>
                    Math.Max(0, v.NumberOfPeople)
                );

        var cateringPeopleNotYetIn =
            cateringVendors
                .Where(v =>
                    !v.CheckedIn &&
                    !v.CheckedOut
                )
                .Sum(v =>
                    Math.Max(0, v.NumberOfPeople)
                );

        // =====================================
        // COMBINED PASS / TICKET TOTALS
        // =====================================

        // One trader or catering pass counts as ONE pass,
        // regardless of how many people are attending.
        var total =
            normalTickets +
            traderPasses +
            cateringPasses;

        var combinedTraderPasses =
            traderPasses +
            cateringPasses;

        // People expected is the normal ticket count
        // plus the people covered by trader and catering passes.
        var peopleExpected =
            normalTickets +
            traderPeopleExpected +
            cateringPeopleExpected;

        // People currently checked in/on site.
        var peopleCheckedIn =
            normalCheckedIn +
            traderPeopleCheckedIn +
            cateringPeopleCheckedIn;

        // People who have checked out.
        var peopleCheckedOut =
            normalCheckedOut +
            traderPeopleCheckedOut +
            cateringPeopleCheckedOut;

        // People physically on site.
        var peopleOnSite =
            normalOnSite +
            traderPeopleCheckedIn +
            cateringPeopleCheckedIn;

        // People who have not arrived yet.
        var peopleNotYetIn =
            normalNotYetIn +
            traderPeopleNotYetIn +
            cateringPeopleNotYetIn;

        // =====================================
        // RESPONSE
        // =====================================

        var response =
            req.CreateResponse(
                HttpStatusCode.OK
            );

        await response.WriteAsJsonAsync(
            new
            {
                EventId = eventId,

                // -----------------------------
                // PASS / TICKET COUNTS
                // -----------------------------

                Total = total,

                NormalTickets = normalTickets,

                TraderPasses = combinedTraderPasses,

                // -----------------------------
                // PEOPLE COUNTS
                // -----------------------------

                PeopleExpected = peopleExpected,

                PeopleCheckedIn = peopleCheckedIn,

                PeopleCheckedOut = peopleCheckedOut,

                PeopleOnSite = peopleOnSite,

                PeopleNotYetIn = peopleNotYetIn,

                // -----------------------------
                // EXISTING STATS
                // -----------------------------

                CheckedIn = peopleCheckedIn,

                CheckedOut = peopleCheckedOut,

                OnSite = peopleOnSite,

                OnSiteUnder14 = onSiteUnder14,

                OnSiteOver14 = onSiteOver14,

                NotYetIn = peopleNotYetIn,

                Cancelled = cancelled
            }
        );

        return response;
    }
}