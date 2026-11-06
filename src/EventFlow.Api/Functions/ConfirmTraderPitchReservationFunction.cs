using Azure;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace EventFlow.Api.Functions;

public class ConfirmTraderPitchReservationFunction
{
    private readonly ITraderPitchRepository _pitches;

    public ConfirmTraderPitchReservationFunction(
        ITraderPitchRepository pitches)
    {
        _pitches = pitches;
    }

    [Function("ConfirmTraderPitchReservation")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "traderpitches/confirm")]
        HttpRequestData req)
    {
        try
        {
            var request =
                await JsonSerializer.DeserializeAsync<ConfirmRequest>(
                    req.Body,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

            if (request == null)
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "Invalid request.");
            }

            if (string.IsNullOrWhiteSpace(
                    request.EventId))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "EventId is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    request.ReservationId))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "ReservationId is required.");
            }

            if (string.IsNullOrWhiteSpace(
                    request.TraderPassNumber))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "TraderPassNumber is required.");
            }

            // ==========================================
            // LOAD ALL PITCHES FOR EVENT
            // ==========================================

            var allPitches =
                await _pitches.GetAllAsync(
                    request.EventId);

            // ==========================================
            // FIND THIS RESERVATION
            // ==========================================

            var reservedPitches =
                allPitches
                    .Where(p =>
                        p.Status == "Reserved" &&
                        string.Equals(
                            p.ReservationId,
                            request.ReservationId,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (reservedPitches.Count == 0)
            {
                return await Error(
                    req,
                    HttpStatusCode.Conflict,
                    "The pitch reservation could not be found or has expired.");
            }

            // ==========================================
            // CHECK RESERVATION EXPIRY
            // ==========================================

            var now =
                DateTime.UtcNow;

            var expired =
                reservedPitches.Any(p =>
                    !p.ReservedUntil.HasValue ||
                    p.ReservedUntil.Value <= now);

            if (expired)
            {
                return await Error(
                    req,
                    HttpStatusCode.Conflict,
                    "The pitch reservation has expired.");
            }

            // ==========================================
            // OPTIONAL ORDER CHECK
            // ==========================================

            if (!string.IsNullOrWhiteSpace(
                    request.OrderId))
            {
                var wrongOrder =
                    reservedPitches.Any(p =>
                        !string.Equals(
                            p.OrderId,
                            request.OrderId,
                            StringComparison.OrdinalIgnoreCase));

                if (wrongOrder)
                {
                    return await Error(
                        req,
                        HttpStatusCode.Conflict,
                        "The reservation does not belong to this order.");
                }
            }

            // ==========================================
            // ALREADY CONFIRMED?
            // ==========================================

            var alreadyBooked =
                reservedPitches.All(p =>
                    p.Status == "Booked" &&
                    string.Equals(
                        p.TraderPassNumber,
                        request.TraderPassNumber,
                        StringComparison.OrdinalIgnoreCase));

            if (alreadyBooked)
            {
                var existing =
                    req.CreateResponse(
                        HttpStatusCode.OK);

                await existing.WriteAsJsonAsync(
                    new
                    {
                        success = true,
                        alreadyBooked = true,
                        eventId = request.EventId,
                        reservationId =
                            request.ReservationId,
                        traderPassNumber =
                            request.TraderPassNumber,
                        pitchIds =
                            reservedPitches
                                .Select(p => p.PitchId)
                                .OrderBy(x => x)
                                .ToList()
                    });

                return existing;
            }

            // ==========================================
            // CONVERT RESERVATION TO BOOKED
            // ==========================================

            foreach (var pitch in reservedPitches)
            {
                pitch.Status = "Booked";

                pitch.TraderPassNumber =
                    request.TraderPassNumber;

                if (!string.IsNullOrWhiteSpace(
                        request.CompanyName))
                {
                    pitch.CompanyName =
                        request.CompanyName;
                }

                if (!string.IsNullOrWhiteSpace(
                        request.OrderId))
                {
                    pitch.OrderId =
                        request.OrderId;
                }

                pitch.ReservedUntil = null;

                pitch.Updated =
                    now;
            }

            // ==========================================
            // ATOMIC UPDATE
            // ==========================================

            try
            {
                await _pitches.AllocateAsync(
                    reservedPitches);
            }
            catch (RequestFailedException ex)
                when (ex.Status == 412)
            {
                return await Error(
                    req,
                    HttpStatusCode.Conflict,
                    "The pitch reservation changed before it could be confirmed. Please try again.");
            }

            // ==========================================
            // SUCCESS
            // ==========================================

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK);

            await response.WriteAsJsonAsync(
                new
                {
                    success = true,
                    alreadyBooked = false,
                    eventId = request.EventId,
                    reservationId =
                        request.ReservationId,
                    traderPassNumber =
                        request.TraderPassNumber,
                    companyName =
                        request.CompanyName,
                    orderId =
                        request.OrderId,
                    pitchIds =
                        reservedPitches
                            .Select(p => p.PitchId)
                            .OrderBy(x => x)
                            .ToList()
                });

            return response;
        }
        catch (Exception ex)
        {
            return await Error(
                req,
                HttpStatusCode.InternalServerError,
                ex.Message);
        }
    }

    private static async Task<HttpResponseData> Error(
        HttpRequestData req,
        HttpStatusCode status,
        string message)
    {
        var response =
            req.CreateResponse(status);

        await response.WriteAsJsonAsync(
            new
            {
                success = false,
                error = message
            });

        return response;
    }

    private class ConfirmRequest
    {
        public string EventId { get; set; } = "";

        public string ReservationId { get; set; } = "";

        public string TraderPassNumber { get; set; } = "";

        public string? CompanyName { get; set; }

        public string? OrderId { get; set; }
    }
}