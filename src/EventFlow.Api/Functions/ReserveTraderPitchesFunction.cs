using Azure;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace EventFlow.Api.Functions;

public class ReserveTraderPitchesFunction
{
    private readonly ITraderPitchRepository _pitches;

    public ReserveTraderPitchesFunction(
        ITraderPitchRepository pitches)
    {
        _pitches = pitches;
    }


    [Function("ReserveTraderPitches")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "traderpitches/reserve/{eventId}")]
        HttpRequestData req,
        string eventId)
    {
        try
        {
            var request =
                await JsonSerializer.DeserializeAsync<ReservationRequest>(
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


            // ==========================================
            // RESERVATION REFERENCE
            // ==========================================

            if (string.IsNullOrWhiteSpace(
                    request.ReservationReference))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "ReservationReference is required.");
            }


            // ==========================================
            // COMPANY
            // ==========================================

            if (string.IsNullOrWhiteSpace(
                    request.CompanyName))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "CompanyName is required.");
            }


            // ==========================================
            // PITCH IDS
            // ==========================================

            if (request.PitchIds == null ||
                request.PitchIds.Count == 0)
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "At least one PitchId is required.");
            }


            // ==========================================
            // NORMALISE PITCH SIZE
            // ==========================================

            var pitchSize =
                (request.PitchSize ?? "")
                .Replace(" ", "")
                .ToLowerInvariant();


            int requiredSquares;


            if (pitchSize == "4x4")
            {
                requiredSquares = 1;
            }
            else if (pitchSize == "8x4")
            {
                requiredSquares = 2;
            }
            else
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "PitchSize must be 4x4 or 8x4.");
            }


            // ==========================================
            // CHECK NUMBER OF PITCHES
            // ==========================================

            if (request.PitchIds.Count !=
                requiredSquares)
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    $"A {request.PitchSize} pitch requires {requiredSquares} space(s).");
            }


            // ==========================================
            // NORMALISE IDS
            // ==========================================

            var pitchIds =
                request.PitchIds
                    .Select(x =>
                        (x ?? "")
                        .Trim()
                        .ToUpperInvariant())
                    .ToList();


            // ==========================================
            // CHECK DUPLICATES
            // ==========================================

            if (pitchIds.Count !=
                pitchIds.Distinct().Count())
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "Duplicate pitch IDs were supplied.");
            }


            // ==========================================
            // VALIDATE IDS
            // ==========================================

            foreach (var pitchId in pitchIds)
            {
                if (!IsValidPitchId(pitchId))
                {
                    return await Error(
                        req,
                        HttpStatusCode.BadRequest,
                        $"Invalid pitch ID: {pitchId}");
                }
            }


            // ==========================================
            // VALIDATE 8x4
            // ==========================================

            if (requiredSquares == 2 &&
                !IsValidEightByFourPair(
                    pitchIds[0],
                    pitchIds[1]))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "The selected spaces are not a valid 8m x 4m pitch combination.");
            }


            // ==========================================
            // LOAD PITCHES
            // ==========================================

            var pitches =
                new List<TraderPitchEntity>();


            foreach (var pitchId in pitchIds)
            {
                var pitch =
                    await _pitches.GetAsync(
                        eventId,
                        pitchId);


                if (pitch == null)
                {
                    return await Error(
                        req,
                        HttpStatusCode.NotFound,
                        $"Pitch {pitchId} does not exist.");
                }


                pitches.Add(pitch);
            }


            // ==========================================
            // CHECK CURRENT STATE
            // ==========================================

            var now =
                DateTime.UtcNow;


            foreach (var pitch in pitches)
            {
                // --------------------------------------
                // ALREADY BOOKED
                // --------------------------------------

                if (pitch.Status == "Booked")
                {
                    return await Error(
                        req,
                        HttpStatusCode.Conflict,
                        $"Pitch {pitch.PitchId} is already booked.");
                }


                // --------------------------------------
                // ACTIVE RESERVATION
                // --------------------------------------

                if (pitch.Status == "Reserved" &&
                    pitch.ReservedUntil.HasValue &&
                    pitch.ReservedUntil.Value > now)
                {
                    return await Error(
                        req,
                        HttpStatusCode.Conflict,
                        $"Pitch {pitch.PitchId} is currently reserved.");
                }


                // --------------------------------------
                // EXPIRED RESERVATION
                // --------------------------------------

                if (pitch.Status == "Reserved" &&
                    (
                        !pitch.ReservedUntil.HasValue ||
                        pitch.ReservedUntil.Value <= now
                    ))
                {
                    // Treat expired reservation as available.

                    pitch.Status =
                        "Available";

                    pitch.ReservationId =
                        "";

                    pitch.ReservedUntil =
                        null;

                    pitch.TraderPassNumber =
                        "";

                    pitch.CompanyName =
                        "";

                    pitch.OrderId =
                        "";

                    pitch.PitchSize =
                        "";

                    pitch.SquaresUsed =
                        1;
                }
            }


            // ==========================================
            // CREATE RESERVATION
            // ==========================================

            var reservationId =
                Guid.NewGuid().ToString("N");


            var reservedUntil =
                now.AddMinutes(5);


            foreach (var pitch in pitches)
            {
                pitch.Status =
                    "Reserved";


                pitch.ReservationId =
                    reservationId;


                pitch.ReservedUntil =
                    reservedUntil;


                // IMPORTANT:
                // No Trader Pass exists yet.

                pitch.TraderPassNumber =
                    "";


                pitch.CompanyName =
                    request.CompanyName;


                // This remains blank until
                // the WooCommerce order exists.

                pitch.OrderId =
                    "";


                pitch.PitchSize =
                    request.PitchSize ?? "";


                pitch.SquaresUsed =
                    requiredSquares;


                pitch.Updated =
                    now;
            }


            // ==========================================
            // ATOMIC RESERVATION
            // ==========================================

            try
            {
                await _pitches.ReserveAsync(
                    pitches);
            }
            catch (RequestFailedException ex)
                when (ex.Status == 412)
            {
                return await Error(
                    req,
                    HttpStatusCode.Conflict,
                    "One or more of the selected pitches were reserved by another customer. Please choose another location.");
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

                    reservationId,

                    reservationReference =
                        request.ReservationReference,

                    eventId,

                    companyName =
                        request.CompanyName,

                    pitchSize =
                        request.PitchSize,

                    pitchIds,

                    reservedUntil,

                    reservationSeconds = 300
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


    // ==========================================
    // VALIDATE PITCH ID
    // ==========================================

    private static bool IsValidPitchId(
        string pitchId)
    {
        if (string.IsNullOrWhiteSpace(
                pitchId))
        {
            return false;
        }


        if (pitchId.Length < 2)
        {
            return false;
        }


        var row =
            pitchId[0];


        // Customer-facing rows are A-E.

        if (row < 'A' ||
            row > 'E')
        {
            return false;
        }


        if (!int.TryParse(
                pitchId.Substring(1),
                out var number))
        {
            return false;
        }


        return number >= 1 &&
               number <= 10;
    }


    // ==========================================
    // VALIDATE 8x4
    // ==========================================

    private static bool IsValidEightByFourPair(
        string first,
        string second)
    {
        if (!TryParsePitch(
                first,
                out var row1,
                out var number1))
        {
            return false;
        }


        if (!TryParsePitch(
                second,
                out var row2,
                out var number2))
        {
            return false;
        }


        // --------------------------------------
        // NORMAL HORIZONTAL PAIR
        // --------------------------------------

        if (row1 == row2 &&
            Math.Abs(
                number1 - number2) == 1)
        {
            return true;
        }


        // --------------------------------------
        // SPECIAL END-ON PAIRS
        // --------------------------------------

        return IsSpecialEndPair(
            row1,
            number1,
            row2,
            number2);
    }


    // ==========================================
    // SPECIAL END-ON
    // ==========================================

    private static bool IsSpecialEndPair(
        char row1,
        int number1,
        char row2,
        int number2)
    {
        return

            // A1 + B1
            (
                row1 == 'A' &&
                number1 == 1 &&
                row2 == 'B' &&
                number2 == 1
            )

            ||

            (
                row1 == 'B' &&
                number1 == 1 &&
                row2 == 'A' &&
                number2 == 1
            )

            ||

            // A10 + B10
            (
                row1 == 'A' &&
                number1 == 10 &&
                row2 == 'B' &&
                number2 == 10
            )

            ||

            (
                row1 == 'B' &&
                number1 == 10 &&
                row2 == 'A' &&
                number2 == 10
            )

            ||

            // C1 + D1
            (
                row1 == 'C' &&
                number1 == 1 &&
                row2 == 'D' &&
                number2 == 1
            )

            ||

            (
                row1 == 'D' &&
                number1 == 1 &&
                row2 == 'C' &&
                number2 == 1
            )

            ||

            // C10 + D10
            (
                row1 == 'C' &&
                number1 == 10 &&
                row2 == 'D' &&
                number2 == 10
            )

            ||

            (
                row1 == 'D' &&
                number1 == 10 &&
                row2 == 'C' &&
                number2 == 10
            );
    }


    // ==========================================
    // PARSE PITCH
    // ==========================================

    private static bool TryParsePitch(
        string pitchId,
        out char row,
        out int number)
    {
        row = '\0';
        number = 0;


        if (!IsValidPitchId(
                pitchId))
        {
            return false;
        }


        row =
            pitchId[0];


        return int.TryParse(
            pitchId.Substring(1),
            out number);
    }


    // ==========================================
    // ERROR
    // ==========================================

    private static async Task<HttpResponseData> Error(
        HttpRequestData req,
        HttpStatusCode status,
        string message)
    {
        var response =
            req.CreateResponse(
                status);


        await response.WriteAsJsonAsync(
            new
            {
                success = false,
                error = message
            });


        return response;
    }


    // ==========================================
    // REQUEST
    // ==========================================

    private class ReservationRequest
    {
        public string ReservationReference
        {
            get;
            set;
        } = "";


        public string CompanyName
        {
            get;
            set;
        } = "";


        public string PitchSize
        {
            get;
            set;
        } = "";


        public List<string> PitchIds
        {
            get;
            set;
        } = new();
    }
}