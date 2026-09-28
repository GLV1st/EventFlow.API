using GateKeeper.Api.Wallet;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;

namespace GateKeeper.Api.Functions;

public sealed class WalletFunction
{
    private readonly ITicketRepository _tickets;
    private readonly IEventRepository _events;
    private readonly AppleWalletService _apple;
    private readonly GoogleWalletService _google;

    public WalletFunction(
        ITicketRepository tickets,
        IEventRepository events,
        AppleWalletService apple,
        GoogleWalletService google)
    {
        _tickets = tickets;
        _events = events;
        _apple = apple;
        _google = google;
    }

    [Function("AppleWalletTicket")]
    public async Task<HttpResponseData> Apple(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "wallet/apple/{eventId}/{ticketNumber}")] HttpRequestData req,
        string eventId,
        string ticketNumber)
    {
        try
        {
            var ticket = await _tickets.GetTicketAsync(eventId, ticketNumber);
            if (ticket == null) return req.CreateResponse(HttpStatusCode.NotFound);
            if (!Authorised(req, ticket.WalletToken)) return req.CreateResponse(HttpStatusCode.Unauthorized);
            var evt = await _events.GetEventAsync(eventId);
            var bytes = await _apple.CreatePassAsync(ticket, evt?.Name ?? eventId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/vnd.apple.pkpass");
            response.Headers.Add("Content-Disposition", $"attachment; filename=\"{ticketNumber}.pkpass\"");
            response.Body.Write(bytes, 0, bytes.Length);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = ex.Message });
            return response;
        }
    }

    [Function("GoogleWalletTicket")]
    public async Task<HttpResponseData> Google(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "wallet/google/{eventId}/{ticketNumber}")] HttpRequestData req,
        string eventId,
        string ticketNumber)
    {
        try
        {
            var ticket = await _tickets.GetTicketAsync(eventId, ticketNumber);
            if (ticket == null) return req.CreateResponse(HttpStatusCode.NotFound);
            if (!Authorised(req, ticket.WalletToken)) return req.CreateResponse(HttpStatusCode.Unauthorized);
            var evt = await _events.GetEventAsync(eventId);
            var url = _google.CreateSaveUrl(ticket, evt?.Name ?? eventId, eventId);
            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Add("Location", url);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = ex.Message });
            return response;
        }
    }
    [Function("AppleWalletBundle")]
    public async Task<HttpResponseData> AppleBundle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "wallet/apple-bundle/{eventId}")] HttpRequestData req,
        string eventId)
    {
        try
        {
            var rawTicketQuery = QueryHelpers.ParseQuery(req.Url.Query).TryGetValue("tickets", out var rawTickets) ? rawTickets.ToString() : "";
            var ticketNumbers = rawTicketQuery
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ticketNumbers.Count == 0) return req.CreateResponse(HttpStatusCode.BadRequest);
            if (ticketNumbers.Count > 10) return req.CreateResponse(HttpStatusCode.BadRequest);

            var rawTokens = QueryHelpers.ParseQuery(req.Url.Query).TryGetValue("tokens", out var tokenValues) ? tokenValues.ToString() : "";
            var tokens = rawTokens.Split(',', StringSplitOptions.None);
            if (tokens.Length != ticketNumbers.Count) return req.CreateResponse(HttpStatusCode.Unauthorized);

            var tickets = new List<GateKeeper.Storage.Entities.TicketEntity>();
            for (var i = 0; i < ticketNumbers.Count; i++)
            {
                var ticket = await _tickets.GetTicketAsync(eventId, ticketNumbers[i]);
                if (ticket == null || !CryptographicEquals(ticket.WalletToken, Uri.UnescapeDataString(tokens[i])))
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                tickets.Add(ticket);
            }

            if (tickets.Count == 0) return req.CreateResponse(HttpStatusCode.NotFound);
            var evt = await _events.GetEventAsync(eventId);
            var bytes = await _apple.CreateBundleAsync(tickets, evt?.Name ?? eventId);
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/vnd.apple.pkpasses");
            response.Headers.Add("Content-Disposition", "attachment; filename=\"GateKeeper-Tickets.pkpasses\"");
            response.Body.Write(bytes, 0, bytes.Length);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = ex.Message });
            return response;
        }
    }

    [Function("GoogleWalletBundle")]
    public async Task<HttpResponseData> GoogleBundle(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "wallet/google-bundle/{eventId}")] HttpRequestData req,
        string eventId)
    {
        try
        {
            var rawTicketQuery = QueryHelpers.ParseQuery(req.Url.Query).TryGetValue("tickets", out var rawTickets) ? rawTickets.ToString() : "";
            var ticketNumbers = rawTicketQuery
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (ticketNumbers.Count == 0) return req.CreateResponse(HttpStatusCode.BadRequest);
            var rawTokens = QueryHelpers.ParseQuery(req.Url.Query).TryGetValue("tokens", out var tokenValues) ? tokenValues.ToString() : "";
            var tokens = rawTokens.Split(',', StringSplitOptions.None);
            if (tokens.Length != ticketNumbers.Count) return req.CreateResponse(HttpStatusCode.Unauthorized);

            var tickets = new List<GateKeeper.Storage.Entities.TicketEntity>();
            for (var i = 0; i < ticketNumbers.Count; i++)
            {
                var ticket = await _tickets.GetTicketAsync(eventId, ticketNumbers[i]);
                if (ticket == null || !CryptographicEquals(ticket.WalletToken, Uri.UnescapeDataString(tokens[i])))
                    return req.CreateResponse(HttpStatusCode.Unauthorized);
                tickets.Add(ticket);
            }

            if (tickets.Count == 0) return req.CreateResponse(HttpStatusCode.NotFound);
            var evt = await _events.GetEventAsync(eventId);
            var url = _google.CreateSaveUrl(tickets, evt?.Name ?? eventId, eventId);
            var response = req.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Add("Location", url);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = ex.Message });
            return response;
        }
    }

    private static bool Authorised(HttpRequestData req, string expected)
    {
        var values = QueryHelpers.ParseQuery(req.Url.Query);
        if (!values.TryGetValue("token", out var supplied)) return false;
        return CryptographicEquals(expected, supplied.ToString());
    }

    private static bool CryptographicEquals(string expected, string supplied)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(supplied)) return false;
        try
        {
            var a = Convert.FromHexString(expected);
            var b = Convert.FromHexString(supplied);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }
        catch
        {
            return false;
        }
    }

}
