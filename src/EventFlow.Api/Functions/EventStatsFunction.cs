
using EventFlow.Api.Security;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class EventStatsFunction
{
    private readonly ITicketRepository _tickets;
    private readonly ITenantSecurityService _tenantSecurity;

    public EventStatsFunction(
        ITicketRepository tickets,
        ITenantSecurityService tenantSecurity)
    {
        _tickets = tickets;
        _tenantSecurity = tenantSecurity;
    }

    [Function("EventStats")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "events/{eventId}/stats")]
        HttpRequestData req,
        string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        // Validate bearer token.
        if (!req.Headers.TryGetValues("Authorization", out var values))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var authHeader = values.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(authHeader) ||
            !authHeader.StartsWith(
                "Bearer ",
                StringComparison.OrdinalIgnoreCase))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var accessToken = authHeader["Bearer ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var access =
            await _tenantSecurity.ValidateEventAccessTokenAsync(accessToken);

        if (access == null || string.IsNullOrWhiteSpace(access.TenantId))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        var tenantId = access.TenantId;

        // Retrieve tickets belonging only to this tenant and event.
        var tickets = await _tickets.GetTicketsByEventAsync(
            tenantId,
            eventId);

        var normalTickets = tickets.Count(t =>
            !string.IsNullOrWhiteSpace(t.OrderId) ||
            t.FirstScanAt != null);

        var checkedIn = tickets.Count(t =>
            t.CheckedIn && !t.Cancelled);

        var checkedOut = tickets.Count(t =>
            t.CheckedOut && !t.Cancelled);

        var onSite = tickets.Count(t =>
            t.CheckedIn &&
            !t.CheckedOut &&
            !t.Cancelled);

        var notYetIn = tickets.Count(t =>
            !t.CheckedIn &&
            !t.CheckedOut &&
            !t.Cancelled);

        var cancelled = tickets.Count(t => t.Cancelled);

        var onSiteUnder14 = tickets.Count(t =>
            t.CheckedIn &&
            !t.CheckedOut &&
            !t.Cancelled &&
            string.Equals(
                t.TicketType?.Trim(),
                "Under 14 Yrs",
                StringComparison.OrdinalIgnoreCase));

        var onSiteOver14 = tickets.Count(t =>
            t.CheckedIn &&
            !t.CheckedOut &&
            !t.Cancelled &&
            string.Equals(
                t.TicketType?.Trim(),
                "Over 14 Yrs",
                StringComparison.OrdinalIgnoreCase));

        var response = req.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(new
        {
            EventId = eventId,

            Total = normalTickets,
            NormalTickets = normalTickets,
            TraderPasses = 0,

            PeopleExpected = normalTickets,
            PeopleCheckedIn = checkedIn,
            PeopleCheckedOut = checkedOut,
            PeopleOnSite = onSite,
            PeopleNotYetIn = notYetIn,

            CheckedIn = checkedIn,
            CheckedOut = checkedOut,
            OnSite = onSite,

            OnSiteUnder14 = onSiteUnder14,
            OnSiteOver14 = onSiteOver14,

            NotYetIn = notYetIn,
            Cancelled = cancelled
        });

        return response;
    }
}
