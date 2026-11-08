
using EventFlow.Api.Security;
using EventFlow.Shared.DTOs;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class CheckOutFunction
{
    private readonly ITicketRepository _tickets;
    private readonly ITicketScanLogRepository _scanLog;
    private readonly ITenantSecurityService _tenantSecurity;

    public CheckOutFunction(
        ITicketRepository tickets,
        ITicketScanLogRepository scanLog,
        ITenantSecurityService tenantSecurity)
    {
        _tickets = tickets;
        _scanLog = scanLog;
        _tenantSecurity = tenantSecurity;
    }

    [Function("CheckOut")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "tickets/checkout")]
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

        var request = await req.ReadFromJsonAsync<CheckOutRequest>();

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

            await response.WriteAsJsonAsync(new CheckOutResponse
            {
                Status = "NOT_FOUND",
                Message = "Ticket not found"
            });

            return response;
        }

        if (ticket.Cancelled)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new CheckOutResponse
            {
                Status = "CANCELLED",
                Message = "Ticket cancelled",
                TicketNumber = ticket.RowKey,
                CustomerName = ticket.CustomerName,
                TicketType = ticket.TicketType
            });

            return response;
        }

        if (ticket.CheckedOut)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new CheckOutResponse
            {
                Status = "ALREADY_CHECKED_OUT",
                Message = "Ticket already checked out",
                TicketNumber = ticket.RowKey,
                CustomerName = ticket.CustomerName,
                TicketType = ticket.TicketType
            });

            return response;
        }

        if (!ticket.CheckedIn)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new CheckOutResponse
            {
                Status = "NOT_CHECKED_IN",
                Message = "Ticket has not been checked in",
                TicketNumber = ticket.RowKey,
                CustomerName = ticket.CustomerName,
                TicketType = ticket.TicketType
            });

            return response;
        }

        var checkoutTime = DateTime.UtcNow;

        ticket.CheckedIn = false;
        ticket.CheckedOut = true;
        ticket.CheckedInAt = null;
        ticket.CheckedOutAt = checkoutTime;
        ticket.Updated = checkoutTime;

        await _tickets.UpdateTicketAsync(tenantId, ticket);

        await SaveScanLogAsync(
            tenantId,
            request.EventId,
            checkoutTime,
            "OUT",
            ticket.TicketType);

        var ok = req.CreateResponse(HttpStatusCode.OK);

        await ok.WriteAsJsonAsync(new CheckOutResponse
        {
            Status = "SUCCESS",
            Message = "Checked out successfully",
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
            Console.WriteLine($"SCAN LOG ERROR: {ex.Message}");
        }
    }
}
