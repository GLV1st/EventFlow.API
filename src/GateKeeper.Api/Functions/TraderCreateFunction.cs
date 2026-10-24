using GateKeeper.Api.Services;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

namespace GateKeeper.Api.Functions;

public class TraderCreateFunction
{
    private readonly ITraderRepository _traderRepository;
    private readonly IEventRepository _eventRepository;

    public TraderCreateFunction(
        ITraderRepository traderRepository,
        IEventRepository eventRepository)
    {
        _traderRepository = traderRepository;
        _eventRepository = eventRepository;
    }

    [Function("TraderCreate")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "traders")]
        HttpRequestData req)
    {
        try
        {
            var trader =
                await JsonSerializer.DeserializeAsync<TraderEntity>(
                    req.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (trader == null)
            {
                var response =
                    req.CreateResponse(
                        HttpStatusCode.BadRequest);

                await response.WriteStringAsync(
                    "Invalid trader data.");

                return response;
            }

            // =====================================
            // REQUIRED VALUES
            // =====================================

            if (string.IsNullOrWhiteSpace(trader.EventId))
            {
                var response =
                    req.CreateResponse(
                        HttpStatusCode.BadRequest);

                await response.WriteStringAsync(
                    "EventId is required.");

                return response;
            }

            if (!TraderDocumentRequirements.IsKnownCategory(trader.PitchType))
            {
                var response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteAsJsonAsync(new
                {
                    error = "A valid PitchType stall category is required.",
                    categories = TraderDocumentRequirements.Categories
                });
                return response;
            }

            // =====================================
            // GET EXISTING EVENT
            // =====================================

            var eventEntity =
                await _eventRepository.GetEventAsync(
                    trader.EventId);

            if (eventEntity == null)
            {
                var response =
                    req.CreateResponse(
                        HttpStatusCode.BadRequest);

                await response.WriteStringAsync(
                    $"Event '{trader.EventId}' was not found.");

                return response;
            }

            // =====================================
            // GENERATE TRADER PASS NUMBER
            // =====================================
            //
            // The caller does NOT supply this.
            //
            // Example:
            // COP-TRADER-001
            // COP-TRADER-002
            // COP-TRADER-003
            //

            var traderNumber =
                await _eventRepository.ReserveTraderNumberAsync(
                    trader.EventId);

            trader.TraderPassNumber =
                $"COP-TRADER-{traderNumber:000}";

            // =====================================
            // SYSTEM VALUES
            // =====================================

            trader.EventName = eventEntity.Name;

            trader.TraderType = "Trader";

            // =====================================
            // AZURE TABLE KEYS
            // =====================================

            trader.PartitionKey =
                trader.EventId;

            trader.RowKey =
                trader.TraderPassNumber;

            // =====================================
            // DOCUMENT UPLOAD TOKEN
            // =====================================
            //
            // Generate a cryptographically secure
            // random token for the Trader Stall
            // document upload page.
            //
            // This token is NOT the Trader Pass number.
            //

            var tokenBytes = new byte[32];

            RandomNumberGenerator.Fill(tokenBytes);

            trader.DocumentUploadToken =
                Convert.ToBase64String(tokenBytes)
                    .Replace("+", "-")
                    .Replace("/", "_")
                    .Replace("=", "");

            // =====================================
            // DOCUMENT UPLOAD TOKEN EXPIRY
            // =====================================
            //
            // The upload link remains valid for
            // 30 days from creation.
            //

            trader.DocumentUploadTokenExpiresAt =
                DateTimeOffset.UtcNow.AddDays(30);

            // =====================================
            // DOCUMENT VALUES
            // =====================================
            //
            // Documents are uploaded later.
            // Start with empty references.
            //

            trader.RiskAssessment = "";

            trader.PublicLiabilityInsurance = "";

            trader.FoodHandlingCertificate = "";

            // =====================================
            // AUDIT
            // =====================================

            trader.Created =
                DateTime.UtcNow;

            trader.Updated =
                DateTime.UtcNow;

            // =====================================
            // SAVE
            // =====================================

            await _traderRepository.AddTraderAsync(
                trader);

            // =====================================
            // RESPONSE
            // =====================================

            var success =
                req.CreateResponse(
                    HttpStatusCode.Created);

            await success.WriteAsJsonAsync(
                trader);

            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                "=================================");

            Console.WriteLine(
                "TRADER CREATE ERROR");

            Console.WriteLine(
                ex.ToString());

            Console.WriteLine(
                "=================================");

            var response =
                req.CreateResponse(
                    HttpStatusCode.InternalServerError);

            await response.WriteStringAsync(
                "Unable to create trader.");

            return response;
        }
    }
}