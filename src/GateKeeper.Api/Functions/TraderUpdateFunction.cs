using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace GateKeeper.Api.Functions;

public class TraderUpdateFunction
{
    private readonly ITraderRepository _traders;

    public TraderUpdateFunction(ITraderRepository traders)
    {
        _traders = traders;
    }

    [Function("TraderUpdate")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
            Route = "traders/{eventId}/{traderPassNumber}")]
        HttpRequestData req,
        string eventId,
        string traderPassNumber)
    {
        try
        {
            var existing = await _traders.GetTraderAsync(eventId, traderPassNumber);

            if (existing == null)
            {
                return req.CreateResponse(HttpStatusCode.NotFound);
            }

            var incoming = await JsonSerializer.DeserializeAsync<TraderUpdateRequest>(
                req.Body,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

            if (incoming == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            // Only booking details are editable from TraderDashboard.
            existing.CompanyName = incoming.CompanyName?.Trim() ?? "";
            existing.ContactName = incoming.ContactName?.Trim() ?? "";
            existing.CustomerEmail = incoming.CustomerEmail?.Trim() ?? "";
            existing.PhoneNumber = incoming.PhoneNumber?.Trim() ?? "";
            existing.VehicleRegistration = incoming.VehicleRegistration?.Trim() ?? "";
            existing.StallDescription = incoming.StallDescription?.Trim() ?? "";
            existing.CharityNumber = incoming.CharityNumber?.Trim() ?? "";
            existing.PitchType = incoming.PitchType?.Trim() ?? "";
            existing.PitchSize = incoming.PitchSize?.Trim() ?? "";
            existing.NumberOfPeople = Math.Max(0, incoming.NumberOfPeople);
            existing.Updated = DateTime.UtcNow;

            await _traders.UpdateTraderAsync(existing);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(existing);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine("TRADER UPDATE ERROR");
            Console.WriteLine(ex);

            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("Unable to update trader.");
            return response;
        }
    }

    private sealed class TraderUpdateRequest
    {
        public string? CompanyName { get; set; }
        public string? ContactName { get; set; }
        public string? CustomerEmail { get; set; }
        public string? PhoneNumber { get; set; }
        public string? VehicleRegistration { get; set; }
        public string? StallDescription { get; set; }
        public string? CharityNumber { get; set; }
        public string? PitchType { get; set; }
        public string? PitchSize { get; set; }
        public int NumberOfPeople { get; set; }
    }
}
