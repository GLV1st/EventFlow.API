using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace EventFlow.Api.Functions;

public class ReleaseTraderPitchReservationFunction
{
    private readonly ITraderPitchRepository _pitches;

    public ReleaseTraderPitchReservationFunction(
        ITraderPitchRepository pitches)
    {
        _pitches = pitches;
    }

    [Function("ReleaseTraderPitchReservation")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "traderpitches/release")]
        HttpRequestData req)
    {
        try
        {
            var request =
                await JsonSerializer.DeserializeAsync<ReleaseRequest>(
                    req.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (request == null)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Invalid request.");
            }

            var eventId =
                (request.EventId ?? "").Trim();

            if (string.IsNullOrWhiteSpace(eventId))
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "EventId is required.");
            }

            var reservationId =
                (request.ReservationId ?? "").Trim();

            var pitchIds =
                (request.PitchIds ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var companyName =
                (request.CompanyName ?? "").Trim();

            var pitches =
                await _pitches.GetAllAsync(eventId);

            // ---------------------------------------------------------
            // FIRST: use the reservation ID when supplied.
            // ---------------------------------------------------------
            var reservedPitches =
                pitches
                    .Where(p =>
                        string.Equals(
                            p.Status,
                            "Reserved",
                            StringComparison.OrdinalIgnoreCase)
                        &&
                        !string.IsNullOrWhiteSpace(reservationId)
                        &&
                        string.Equals(
                            p.ReservationId,
                            reservationId,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            // ---------------------------------------------------------
            // FALLBACK: if the reservation ID is stale/wrong, find the
            // reservation from the old pitch IDs + company name.
            //
            // This is specifically for replacing a pitch from a live
            // WooCommerce session. It prevents an old reservation ID from
            // stopping a customer changing A1 -> A2, etc.
            // ---------------------------------------------------------
            if (reservedPitches.Count == 0 && pitchIds.Count > 0)
            {
                var matching =
                    pitches
                        .Where(p =>
                            string.Equals(
                                p.Status,
                                "Reserved",
                                StringComparison.OrdinalIgnoreCase)
                            &&
                            pitchIds.Contains(
                                p.PitchId,
                                StringComparer.OrdinalIgnoreCase)
                            &&
                            (
                                string.IsNullOrWhiteSpace(companyName)
                                ||
                                string.Equals(
                                    p.CompanyName,
                                    companyName,
                                    StringComparison.OrdinalIgnoreCase)
                            )
                            &&
                            !string.IsNullOrWhiteSpace(p.ReservationId))
                        .ToList();

                // Only use the fallback when ALL requested old pitch squares
                // point to one and the same reservation.
                var reservationGroups =
                    matching
                        .GroupBy(p => p.ReservationId)
                        .Where(g =>
                            g.Select(p => p.PitchId)
                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                .Count()
                            == pitchIds.Count)
                        .ToList();

                if (reservationGroups.Count == 1)
                {
                    reservedPitches =
                        reservationGroups[0].ToList();
                    reservationId =
                        reservationGroups[0].Key;
                }
            }

            // ---------------------------------------------------------
            // Nothing to release is still a successful/idempotent result.
            // This is important for expired reservations and clean sessions.
            // ---------------------------------------------------------
            if (reservedPitches.Count == 0)
            {
                return await JsonResponse(
                    req,
                    HttpStatusCode.OK,
                    new
                    {
                        success = true,
                        eventId,
                        reservationId,
                        released = 0,
                        pitchIds = Array.Empty<string>()
                    });
            }

            var releasedIds = new List<string>();

            foreach (var pitch in reservedPitches)
            {
                pitch.Status = "Available";
                pitch.ReservationId = "";
                pitch.ReservedUntil = null;
                pitch.TraderPassNumber = "";
                pitch.CompanyName = "";
                pitch.OrderId = "";
                pitch.PitchSize = "";
                pitch.SquaresUsed = 1;
                pitch.Updated = DateTime.UtcNow;

                await _pitches.UpsertAsync(pitch);
                releasedIds.Add(pitch.PitchId);
            }

            return await JsonResponse(
                req,
                HttpStatusCode.OK,
                new
                {
                    success = true,
                    eventId,
                    reservationId,
                    released = releasedIds.Count,
                    pitchIds = releasedIds
                });
        }
        catch (Exception ex)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                ex.Message);
        }
    }

    private static async Task<HttpResponseData> JsonResponse(
        HttpRequestData req,
        HttpStatusCode status,
        object body)
    {
        var response = req.CreateResponse(status);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(
            JsonSerializer.Serialize(body));
        return response;
    }

    private static async Task<HttpResponseData> ErrorResponse(
        HttpRequestData req,
        HttpStatusCode status,
        string message)
    {
        return await JsonResponse(
            req,
            status,
            new
            {
                success = false,
                error = message
            });
    }

    private sealed class ReleaseRequest
    {
        public string? EventId { get; set; }
        public string? ReservationId { get; set; }
        public List<string>? PitchIds { get; set; }
        public string? CompanyName { get; set; }
    }
}
