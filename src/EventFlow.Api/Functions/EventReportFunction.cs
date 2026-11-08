
using EventFlow.Api.Security;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class EventReportFunction
{
    private readonly IEventRepository _events;
    private readonly ITicketRepository _tickets;
    private readonly ITicketScanLogRepository _scanLog;
    private readonly ITenantSecurityService _tenantSecurity;

    public EventReportFunction(
        IEventRepository events,
        ITicketRepository tickets,
        ITicketScanLogRepository scanLog,
        ITenantSecurityService tenantSecurity)
    {
        _events = events;
        _tickets = tickets;
        _scanLog = scanLog;
        _tenantSecurity = tenantSecurity;
    }

    [Function("EventReport")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "events/{eventId}/report")]
        HttpRequestData req,
        string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        // Validate bearer token and obtain the authenticated tenant.
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

        var tenantId =
            await _tenantSecurity.ValidateAccessTokenAsync(accessToken);

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return req.CreateResponse(HttpStatusCode.Unauthorized);
        }

        // All data retrieval is scoped to the authenticated tenant.
        var eventEntity = await _events.GetEventAsync(tenantId, eventId);
        var tickets = await _tickets.GetTicketsByEventAsync(tenantId, eventId);
        var scans = await _scanLog.GetScansByEventAsync(tenantId, eventId);

        var soldTickets = tickets
            .Where(t =>
                !t.Cancelled &&
                (!string.IsNullOrWhiteSpace(t.OrderId) ||
                 t.FirstScanAt != null))
            .ToList();

        var cancelledTickets = tickets.Count(t => t.Cancelled);

        var ticketTypeRows = soldTickets
            .GroupBy(
                t => string.IsNullOrWhiteSpace(t.TicketType)
                    ? "Unspecified"
                    : t.TicketType.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                TicketType = g.Key,
                Sold = g.Count()
            })
            .ToList();

        var scanTypeRows = scans
            .GroupBy(
                s => string.IsNullOrWhiteSpace(s.ScanType)
                    ? "Unknown"
                    : s.ScanType.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                ScanType = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var scanTicketTypeRows = scans
            .GroupBy(
                s => string.IsNullOrWhiteSpace(s.TicketType)
                    ? "Unspecified"
                    : s.TicketType.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                TicketType = g.Key,
                Scans = g.Count(),
                In = g.Count(s =>
                    string.Equals(
                        s.ScanType,
                        "IN",
                        StringComparison.OrdinalIgnoreCase)),
                Out = g.Count(s =>
                    string.Equals(
                        s.ScanType,
                        "OUT",
                        StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(x => x.Scans)
            .ToList();

        // Convert UTC timestamps to UK local time.
        var ukTimeZone = GetUkTimeZone();

        var localScans = scans
            .Select(s => new
            {
                Scan = s,
                LocalTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(
                        s.ScanTime,
                        DateTimeKind.Utc),
                    ukTimeZone)
            })
            .OrderBy(x => x.LocalTime)
            .ToList();

        // Group scans into 15-minute periods.
        var busiestPeriods = localScans
            .GroupBy(x => new DateTime(
                x.LocalTime.Year,
                x.LocalTime.Month,
                x.LocalTime.Day,
                x.LocalTime.Hour,
                (x.LocalTime.Minute / 15) * 15,
                0))
            .Select(g => new
            {
                Start = g.Key,
                End = g.Key.AddMinutes(15),
                In = g.Count(x =>
                    string.Equals(
                        x.Scan.ScanType,
                        "IN",
                        StringComparison.OrdinalIgnoreCase)),
                Out = g.Count(x =>
                    string.Equals(
                        x.Scan.ScanType,
                        "OUT",
                        StringComparison.OrdinalIgnoreCase)),
                TotalScans = g.Count()
            })
            .OrderByDescending(x => x.TotalScans)
            .ThenBy(x => x.Start)
            .ToList();

        // Calculate peak attendance from the IN/OUT scan history.
        var peopleOnSite = 0;
        var peakPeopleOnSite = 0;
        DateTime? peakTime = null;

        foreach (var item in localScans)
        {
            if (string.Equals(
                item.Scan.ScanType,
                "IN",
                StringComparison.OrdinalIgnoreCase))
            {
                peopleOnSite++;
            }
            else if (string.Equals(
                item.Scan.ScanType,
                "OUT",
                StringComparison.OrdinalIgnoreCase))
            {
                peopleOnSite = Math.Max(0, peopleOnSite - 1);
            }

            if (peopleOnSite > peakPeopleOnSite)
            {
                peakPeopleOnSite = peopleOnSite;
                peakTime = item.LocalTime;
            }
        }

        var firstScan = localScans.FirstOrDefault()?.LocalTime;
        var lastScan = localScans.LastOrDefault()?.LocalTime;
        var busiestPeriod = busiestPeriods.FirstOrDefault();

        var response = req.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(new
        {
            EventId = eventId,
            EventName = eventEntity?.Name ?? eventId,

            EventDate = soldTickets
                .Select(t => t.EventDate)
                .FirstOrDefault(x =>
                    !string.IsNullOrWhiteSpace(x)) ?? "",

            TicketsSold = soldTickets.Count,
            NormalTicketsSold = soldTickets.Count,

            TraderPassesSold = 0,
            CateringPassesSold = 0,

            CancelledTickets = cancelledTickets,

            TotalScans = scans.Count,

            TotalInScans = scans.Count(s =>
                string.Equals(
                    s.ScanType,
                    "IN",
                    StringComparison.OrdinalIgnoreCase)),

            TotalOutScans = scans.Count(s =>
                string.Equals(
                    s.ScanType,
                    "OUT",
                    StringComparison.OrdinalIgnoreCase)),

            FirstScan = firstScan,
            LastScan = lastScan,

            PeakPeopleOnSite = peakPeopleOnSite,
            PeakTime = peakTime,

            BusiestPeriod = busiestPeriod == null
                ? null
                : new
                {
                    Start = busiestPeriod.Start,
                    End = busiestPeriod.End,
                    In = busiestPeriod.In,
                    Out = busiestPeriod.Out,
                    TotalScans = busiestPeriod.TotalScans
                },

            TicketTypes = ticketTypeRows
                .OrderBy(x => x.TicketType)
                .Select(x => new
                {
                    x.TicketType,
                    x.Sold,

                    Scanned = scanTicketTypeRows
                        .Where(s => string.Equals(
                            s.TicketType,
                            x.TicketType,
                            StringComparison.OrdinalIgnoreCase))
                        .Select(s => s.Scans)
                        .FirstOrDefault(),

                    In = scanTicketTypeRows
                        .Where(s => string.Equals(
                            s.TicketType,
                            x.TicketType,
                            StringComparison.OrdinalIgnoreCase))
                        .Select(s => s.In)
                        .FirstOrDefault(),

                    Out = scanTicketTypeRows
                        .Where(s => string.Equals(
                            s.TicketType,
                            x.TicketType,
                            StringComparison.OrdinalIgnoreCase))
                        .Select(s => s.Out)
                        .FirstOrDefault()
                }),

            ScanTypes = scanTypeRows,

            BusiestPeriods = busiestPeriods
                .OrderBy(x => x.Start)
                .Select(x => new
                {
                    Start = x.Start,
                    End = x.End,
                    x.In,
                    x.Out,
                    x.TotalScans
                })
        });

        return response;
    }

    private static TimeZoneInfo GetUkTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Europe/London");
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(
                "GMT Standard Time");
        }
    }
}
