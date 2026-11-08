using Azure;
using EventFlow.Api.Security;
using EventFlow.Shared.DTOs;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class GenerateTicketsFunction
{
    private readonly IEventRepository _events;
    private readonly ITicketRepository _tickets;
    private readonly ITenantSecurityService _tenantSecurity;

    public GenerateTicketsFunction(IEventRepository events, ITicketRepository tickets, ITenantSecurityService tenantSecurity)
    {
        _events = events;
        _tickets = tickets;
        _tenantSecurity = tenantSecurity;
    }

    [Function("GenerateTickets")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "tickets/generate")] HttpRequestData req)
    {
        if (!req.Headers.TryGetValues("Authorization", out var values))
            return req.CreateResponse(HttpStatusCode.Unauthorized);

        var authHeader = values.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return req.CreateResponse(HttpStatusCode.Unauthorized);

        var accessToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(accessToken))
            return req.CreateResponse(HttpStatusCode.Unauthorized);

        var tenantId = await _tenantSecurity.ValidateAccessTokenAsync(accessToken);
        if (string.IsNullOrWhiteSpace(tenantId))
            return req.CreateResponse(HttpStatusCode.Unauthorized);

        var request = await req.ReadFromJsonAsync<GenerateTicketsRequest>();
        if (request is null)
            return await ErrorResponse(req, HttpStatusCode.BadRequest, "Invalid request.");

        if (string.IsNullOrWhiteSpace(request.EventId) || string.IsNullOrWhiteSpace(request.EventName) ||
            string.IsNullOrWhiteSpace(request.TicketPrefix) || string.IsNullOrWhiteSpace(request.OrderId) ||
            string.IsNullOrWhiteSpace(request.OrderItemId))
            return await ErrorResponse(req, HttpStatusCode.BadRequest, "EventId, EventName, TicketPrefix, OrderId and OrderItemId are required.");

        if (request.Quantity <= 0 || request.Quantity > 99)
            return await ErrorResponse(req, HttpStatusCode.BadRequest, "Quantity must be between 1 and 99.");

        var eventId = request.EventId.Trim();
        var evt = await _events.GetEventAsync(tenantId, eventId);

        // First purchase automatically creates the event for this tenant.
        if (evt is null)
        {
            var newEvent = new EventEntity
            {
                TenantId = tenantId,
                PartitionKey = tenantId,
                RowKey = eventId,
                Name = request.EventName.Trim(),
                TicketPrefix = request.TicketPrefix.Trim().ToUpperInvariant(),
                NextTicketNumber = 1
            };
            try
            {
                await _events.AddEventAsync(tenantId, newEvent);
                evt = newEvent;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                evt = await _events.GetEventAsync(tenantId, eventId);
                if (evt is null)
                    return await ErrorResponse(req, HttpStatusCode.Conflict, "Event creation conflict. Please retry.");
            }
        }

        if (string.IsNullOrWhiteSpace(evt.TicketPrefix))
            return await ErrorResponse(req, HttpStatusCode.InternalServerError, "Event has no ticket prefix.");

        // Avoid reserving new numbers for ordinary sequential WooCommerce webhook retries.
        var existingTickets = await _tickets.GetTicketsForOrderLineAsync(
            tenantId, eventId, request.OrderId, request.OrderItemId);

        if (existingTickets is not null)
        {
            var existingResponse = existingTickets.Select(t => new GenerateTicketsResponse
            {
                TicketNumber = t.RowKey
            }).ToList();

            var existingHttp = req.CreateResponse(HttpStatusCode.OK);
            await existingHttp.WriteAsJsonAsync(existingResponse);
            return existingHttp;
        }

        // Reserve numbers before the atomic ticket/order-line transaction. A losing concurrent retry can
        // leave harmless gaps, but cannot create duplicate tickets or return a second ticket set.
        int firstTicketNumber;
        try
        {
            firstTicketNumber = await _events.ReserveTicketNumbersAsync(tenantId, eventId, request.Quantity);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED TO RESERVE TICKET NUMBERS: {ex}");
            return await ErrorResponse(req, HttpStatusCode.InternalServerError, "Unable to reserve ticket numbers.");
        }

        var now = DateTime.UtcNow;
        var tickets = new List<TicketEntity>(request.Quantity);
        for (var i = 0; i < request.Quantity; i++)
        {
            tickets.Add(new TicketEntity
            {
                TenantId = tenantId,
                EventId = eventId,
                PartitionKey = $"{tenantId}|{eventId}",
                RowKey = $"{evt.TicketPrefix}-{(firstTicketNumber + i):D6}",
                OrderId = request.OrderId.Trim(),
                OrderItemId = request.OrderItemId.Trim(),
                TicketType = request.TicketType,
                EventDate = request.EventDate,
                CustomerName = request.CustomerName,
                CustomerEmail = request.CustomerEmail,
                CheckedIn = false,
                CheckedOut = false,
                Cancelled = false,
                Created = now,
                Updated = now
            });
        }

        try
        {
            var committedTickets = await _tickets.CreateTicketsForOrderLineAsync(
                tenantId, eventId, request.OrderId, request.OrderItemId, tickets);

            var response = committedTickets.Select(t => new GenerateTicketsResponse
            {
                TicketNumber = t.RowKey
            }).ToList();

            var http = req.CreateResponse(HttpStatusCode.OK);
            await http.WriteAsJsonAsync(response);
            return http;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"FAILED TO CREATE TICKETS: {ex}");
            return await ErrorResponse(req, HttpStatusCode.InternalServerError, "Unable to create tickets.");
        }
    }

    private static async Task<HttpResponseData> ErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
    {
        var response = req.CreateResponse(status);
        await response.WriteAsJsonAsync(new { Status = "ERROR", Message = message });
        return response;
    }
}
