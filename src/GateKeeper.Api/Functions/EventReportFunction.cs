using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class EventReportFunction
{
    private readonly IEventRepository _events;
    private readonly ITicketRepository _tickets;
    private readonly ITraderRepository _traders;
    private readonly ICateringVendorRepository _cateringVendors;
    private readonly ITicketScanLogRepository _scanLog;

    public EventReportFunction(
        IEventRepository events,
        ITicketRepository tickets,
        ITraderRepository traders,
        ICateringVendorRepository cateringVendors,
        ITicketScanLogRepository scanLog)
    {
        _events = events;
        _tickets = tickets;
        _traders = traders;
        _cateringVendors = cateringVendors;
        _scanLog = scanLog;
    }

    [Function("EventReport")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "events/{eventId}/report"
        )]
        HttpRequestData req,
        string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        var eventEntity = await _events.GetEventAsync(eventId);
        var tickets = await _tickets.GetTicketsByEventAsync(eventId);
        var traders = await _traders.GetTradersByEventAsync(eventId);
        var catering = await _cateringVendors.GetCateringVendorsByEventAsync(eventId);
        var scans = await _scanLog.GetScansByEventAsync(eventId);

        // Normal tickets are considered sold when they have a WooCommerce
        // order OR have been successfully scanned as a generated physical ticket.
        var soldTickets = tickets
            .Where(t => !t.Cancelled &&
                        (!string.IsNullOrWhiteSpace(t.OrderId) || t.FirstScanAt != null))
            .ToList();

        var cancelledTickets = tickets.Count(t => t.Cancelled);

        var ticketTypeRows = soldTickets
            .GroupBy(t => string.IsNullOrWhiteSpace(t.TicketType) ? "Unspecified" : t.TicketType.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                TicketType = g.Key,
                Sold = g.Count()
            })
            .ToList();

        // Trader/catering passes are sold passes, while the scan log records
        // each person entering/leaving under those passes.
        if (traders.Count > 0)
        {
            ticketTypeRows.Add(new
            {
                TicketType = "Trader Pass",
                Sold = traders.Count
            });
        }

        if (catering.Count > 0)
        {
            ticketTypeRows.Add(new
            {
                TicketType = "Catering",
                Sold = catering.Count
            });
        }

        var scanTypeRows = scans
            .GroupBy(s => string.IsNullOrWhiteSpace(s.ScanType) ? "Unknown" : s.ScanType.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                ScanType = g.Key,
                Count = g.Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var scanTicketTypeRows = scans
            .GroupBy(s => string.IsNullOrWhiteSpace(s.TicketType) ? "Unspecified" : s.TicketType.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new
            {
                TicketType = g.Key,
                Scans = g.Count(),
                In = g.Count(s => string.Equals(s.ScanType, "IN", StringComparison.OrdinalIgnoreCase)),
                Out = g.Count(s => string.Equals(s.ScanType, "OUT", StringComparison.OrdinalIgnoreCase))
            })
            .OrderByDescending(x => x.Scans)
            .ToList();

        // Convert UTC scan timestamps to UK local time for the human report.
        var ukTimeZone = GetUkTimeZone();

        var localScans = scans
            .Select(s => new
            {
                Scan = s,
                LocalTime = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(s.ScanTime, DateTimeKind.Utc),
                    ukTimeZone)
            })
            .OrderBy(x => x.LocalTime)
            .ToList();

        // Group successful scans into 15-minute periods.
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
                In = g.Count(x => string.Equals(x.Scan.ScanType, "IN", StringComparison.OrdinalIgnoreCase)),
                Out = g.Count(x => string.Equals(x.Scan.ScanType, "OUT", StringComparison.OrdinalIgnoreCase)),
                TotalScans = g.Count()
            })
            .OrderByDescending(x => x.TotalScans)
            .ThenBy(x => x.Start)
            .ToList();

        // Work out peak people onsite from the historical IN/OUT stream.
        var peopleOnSite = 0;
        var peakPeopleOnSite = 0;
        DateTime? peakTime = null;

        foreach (var item in localScans)
        {
            if (string.Equals(item.Scan.ScanType, "IN", StringComparison.OrdinalIgnoreCase))
                peopleOnSite++;
            else if (string.Equals(item.Scan.ScanType, "OUT", StringComparison.OrdinalIgnoreCase))
                peopleOnSite = Math.Max(0, peopleOnSite - 1);

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
            EventDate = soldTickets.Select(t => t.EventDate).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "",
            TicketsSold = ticketTypeRows.Sum(x => x.Sold),
            NormalTicketsSold = soldTickets.Count,
            TraderPassesSold = traders.Count,
            CateringPassesSold = catering.Count,
            CancelledTickets = cancelledTickets,
            TotalScans = scans.Count,
            TotalInScans = scans.Count(s => string.Equals(s.ScanType, "IN", StringComparison.OrdinalIgnoreCase)),
            TotalOutScans = scans.Count(s => string.Equals(s.ScanType, "OUT", StringComparison.OrdinalIgnoreCase)),
            FirstScan = firstScan,
            LastScan = lastScan,
            PeakPeopleOnSite = peakPeopleOnSite,
            PeakTime = peakTime,
            BusiestPeriod = busiestPeriod == null ? null : new
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
                        .Where(s => string.Equals(s.TicketType, x.TicketType, StringComparison.OrdinalIgnoreCase))
                        .Select(s => s.Scans)
                        .FirstOrDefault(),
                    In = scanTicketTypeRows
                        .Where(s => string.Equals(s.TicketType, x.TicketType, StringComparison.OrdinalIgnoreCase))
                        .Select(s => s.In)
                        .FirstOrDefault(),
                    Out = scanTicketTypeRows
                        .Where(s => string.Equals(s.TicketType, x.TicketType, StringComparison.OrdinalIgnoreCase))
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
            return TimeZoneInfo.FindSystemTimeZoneById("GMT Standard Time");
        }
    }
}
