
using EventFlow.Api.Security;
using EventFlow.Shared.DTOs;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class CheckInFunction
{
    private readonly ITicketRepository _tickets;
    private readonly ITicketScanLogRepository _scanLog;
    private readonly ITenantSecurityService _tenantSecurity;

    public CheckInFunction(
        ITicketRepository tickets,
        ITicketScanLogRepository scanLog,
        ITenantSecurityService tenantSecurity)
    {
        _tickets = tickets;
        _scanLog = scanLog;
        _tenantSecurity = tenantSecurity;
    }

    [Function("CheckIn")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "tickets/checkin")]
        HttpRequestData req)
    {
        // Authenticate the tenant using its bearer token.
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

        var request = await req.ReadFromJsonAsync<CheckInRequest>();

        if (request == null ||
            string.IsNullOrWhiteSpace(request.EventId) ||
            string.IsNullOrWhiteSpace(request.TicketNumber))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        // Retrieve the ticket within the authenticated tenant only.
        var ticket = await _tickets.GetTicketAsync(
            tenantId,
            request.EventId,
            request.TicketNumber);

        if (ticket == null)
        {
            var response = req.CreateResponse(HttpStatusCode.NotFound);

            await response.WriteAsJsonAsync(new CheckInResponse
            {
                Status = "NOT_FOUND",
                Message = "Ticket not found"
            });

            return response;
        }

        if (ticket.Cancelled)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new CheckInResponse
            {
                Status = "CANCELLED",
                Message = "Ticket cancelled",
                TicketNumber = ticket.RowKey,
                CustomerName = ticket.CustomerName,
                TicketType = ticket.TicketType
            });

            return response;
        }

        if (ticket.CheckedIn)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new CheckInResponse
            {
                Status = "ALREADY_CHECKED_IN",
                Message = "Ticket already checked in",
                TicketNumber = ticket.RowKey,
                CustomerName = ticket.CustomerName,
                TicketType = ticket.TicketType
            });

            return response;
        }

        var scanTime = DateTime.UtcNow;

        ticket.CheckedIn = true;
        ticket.CheckedOut = false;
        ticket.CheckedInAt = scanTime;
        ticket.CheckedOutAt = null;

        // FirstScanAt is permanent.
        if (ticket.FirstScanAt == null)
        {
            ticket.FirstScanAt = scanTime;
        }

        ticket.Updated = scanTime;

        await _tickets.UpdateTicketAsync(tenantId, ticket);

        await SaveScanLogAsync(
            tenantId,
            request.EventId,
            scanTime,
            "IN",
            ticket.TicketType);

        var ok = req.CreateResponse(HttpStatusCode.OK);

        await ok.WriteAsJsonAsync(new CheckInResponse
        {
            Status = "SUCCESS",
            Message = "Welcome!",
            TicketNumber = ticket.RowKey,
            CustomerName = ticket.CustomerName,
            TicketType = ticket.TicketType
        });

        return ok;
    }

    private async Task SaveScanLogAsync(
        string tenantId,
        string eventId,
        DateTime scanTime,
        string scanType,
        string ticketType)
    {
        try
        {
            await _scanLog.AddScanAsync(tenantId, new TicketScanLogEntity
            {
                TenantId = tenantId,
                EventId = eventId,
                PartitionKey = $"{tenantId}|{eventId}",
                RowKey = Guid.NewGuid().ToString("N"),
                ScanTime = scanTime,
                ScanType = scanType,
                TicketType = ticketType
            });
        }
        catch (Exception ex)
        {
            // A reporting-log failure must not invalidate a successful scan.
            Console.WriteLine($"SCAN LOG ERROR: {ex.Message}");
        }
    }
}
