using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class EventStatsFunction
{
    private readonly ITicketRepository _tickets;

    public EventStatsFunction(ITicketRepository tickets)
    {
        _tickets = tickets;
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
        // ADMIN AUTHENTICATION
        // =====================================

        var adminToken =
            req.Headers.TryGetValues(
                "X-GateKeeper-Admin",
                out var tokenValues
            )
            ? tokenValues.FirstOrDefault()
            : null;

        var configuredPassword =
            Environment.GetEnvironmentVariable(
                "GateKeeperAdminPassword"
            );

        if (string.IsNullOrWhiteSpace(configuredPassword))
        {
            return await CreateErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "GateKeeperAdminPassword is not configured."
            );
        }

        /*
         * The admin authentication endpoint returns the
         * configured admin password as the temporary token.
         *
         * This keeps the existing app simple while ensuring
         * the stats endpoint itself cannot be accessed without
         * admin authentication.
         */

        if (string.IsNullOrWhiteSpace(adminToken) ||
            adminToken != configuredPassword)
        {
            return await CreateErrorResponse(
                req,
                HttpStatusCode.Unauthorized,
                "Admin authentication required."
            );
        }


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
        // GET ALL TICKETS FOR EVENT
        // =====================================

        var tickets =
            await _tickets.GetTicketsByEventAsync(
                eventId
            );


        // =====================================
        // CALCULATE STATISTICS
        // =====================================

        var total =
            tickets.Count;

        var checkedIn =
            tickets.Count(t => t.CheckedIn);

        var checkedOut =
            tickets.Count(t => t.CheckedOut);

        var onSite =
            tickets.Count(t =>
                t.CheckedIn &&
                !t.CheckedOut
            );

        var notYetIn =
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
        // ON-SITE TICKET TYPE BREAKDOWN
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

                Total = total,

                CheckedIn = checkedIn,

                CheckedOut = checkedOut,

                OnSite = onSite,

                OnSiteUnder14 = onSiteUnder14,

                OnSiteOver14 = onSiteOver14,

                NotYetIn = notYetIn,

                Cancelled = cancelled
            }
        );

        return response;
    }


    // =====================================
    // ERROR RESPONSE
    // =====================================

    private static async Task<HttpResponseData> CreateErrorResponse(
        HttpRequestData req,
        HttpStatusCode status,
        string message)
    {
        var response =
            req.CreateResponse(status);

        await response.WriteAsJsonAsync(
            new
            {
                Authenticated = false,
                Message = message
            }
        );

        return response;
    }
}