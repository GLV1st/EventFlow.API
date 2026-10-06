using Azure;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace GateKeeper.Api.Functions;

public class ReallocateTraderPitchFunction
{
    private readonly ITraderPitchRepository _pitches;
    private readonly ITraderRepository _traders;

    public ReallocateTraderPitchFunction(
        ITraderPitchRepository pitches,
        ITraderRepository traders)
    {
        _pitches = pitches;
        _traders = traders;
    }

    [Function("ReallocateTraderPitch")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "traderpitches/reallocate/{eventId}")]
        HttpRequestData req,
        string eventId)
    {
        try
        {
            // =========================================
            // READ REQUEST
            // =========================================

            var request =
                await JsonSerializer.DeserializeAsync<ReallocationRequest>(
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

            if (string.IsNullOrWhiteSpace(
                    request.TraderPassNumber))
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "TraderPassNumber is required.");
            }

            if (request.NewPitchIds == null ||
                request.NewPitchIds.Count == 0)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "At least one new pitch is required.");
            }

            // =========================================
            // FIND TRADER
            // =========================================

            var trader =
                await _traders.GetTraderAsync(
                    eventId,
                    request.TraderPassNumber);

            if (trader == null)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.NotFound,
                    "Trader could not be found.");
            }

            // =========================================
            // DETERMINE PITCH SIZE
            // =========================================

            var pitchSize =
                trader.PitchSize?
                    .Replace(" ", "")
                    .ToLowerInvariant()
                ?? "";

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
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    $"Trader has an unsupported pitch size: {trader.PitchSize ?? "Unknown"}");
            }

            // =========================================
            // NORMALISE NEW PITCH IDS
            // =========================================

            var newPitchIds =
                request.NewPitchIds
                    .Select(x => x ?? "")
                    .Select(x => x.Trim())
                    .Select(x => x.ToUpperInvariant())
                    .ToList();

            // =========================================
            // REMOVE DUPLICATES
            // =========================================

            if (newPitchIds.Count !=
                newPitchIds.Distinct().Count())
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Duplicate pitch IDs were supplied.");
            }

            // =========================================
            // CHECK NUMBER OF PITCHES
            // =========================================

            if (newPitchIds.Count != requiredSquares)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    $"A {trader.PitchSize ?? "unknown"} pitch requires {requiredSquares} space(s).");
            }

            // =========================================
            // VALIDATE PITCH IDS
            // =========================================

            foreach (var pitchId in newPitchIds)
            {
                if (!IsValidPitchId(pitchId))
                {
                    return await ErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        $"Invalid pitch ID: {pitchId}");
                }
            }

            // =========================================
            // VALIDATE 8 x 4 COMBINATION
            // =========================================

            if (requiredSquares == 2)
            {
                string firstPitch =
                    newPitchIds[0];

                string secondPitch =
                    newPitchIds[1];

                if (!IsValidEightByFourPair(
                        firstPitch,
                        secondPitch))
                {
                    return await ErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        "The selected spaces are not a valid 8m x 4m pitch combination.");
                }
            }

            // =========================================
            // GET ALL CURRENT PITCHES
            // =========================================

            var allPitches =
                await _pitches.GetAllAsync(eventId);

            // =========================================
            // FIND CURRENT TRADER ALLOCATION
            // =========================================

            var traderPassNumber =
                trader.TraderPassNumber ?? "";

            var oldPitches =
                allPitches
                    .Where(p =>
                        p.Status == "Booked" &&
                        string.Equals(
                            p.TraderPassNumber,
                            traderPassNumber,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

            if (oldPitches.Count == 0)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.NotFound,
                    "No current pitch allocation was found for this trader.");
            }

            // =========================================
            // CHECK CURRENT ALLOCATION SIZE
            // =========================================

            if (oldPitches.Count != requiredSquares)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.Conflict,
                    $"The trader currently has {oldPitches.Count} allocated space(s), but their pitch size requires {requiredSquares}.");
            }

            // =========================================
            // CHECK NOT SAME LOCATION
            // =========================================

            var oldIds =
                oldPitches
                    .Select(p =>
                        p.PitchId?
                            .Trim()
                            .ToUpperInvariant()
                        ?? "")
                    .OrderBy(x => x)
                    .ToList();

            var requestedIds =
                newPitchIds
                    .OrderBy(x => x)
                    .ToList();

            if (oldIds.SequenceEqual(requestedIds))
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "The trader is already allocated to these pitch locations.");
            }

            // =========================================
            // LOAD NEW PITCHES
            // =========================================

            var newPitches =
                new List<TraderPitchEntity>();

            foreach (var pitchId in newPitchIds)
            {
                var pitch =
                    await _pitches.GetAsync(
                        eventId,
                        pitchId);

                if (pitch == null)
                {
                    return await ErrorResponse(
                        req,
                        HttpStatusCode.NotFound,
                        $"Pitch {pitchId} does not exist.");
                }

                newPitches.Add(pitch);
            }

            // =========================================
            // CHECK NEW PITCHES AVAILABLE
            // =========================================

            foreach (var pitch in newPitches)
            {
                if (!string.Equals(
                        pitch.Status,
                        "Available",
                        StringComparison.OrdinalIgnoreCase))
                {
                    return await ErrorResponse(
                        req,
                        HttpStatusCode.Conflict,
                        $"Pitch {pitch.PitchId ?? ""} is not available.");
                }
            }

            // =========================================
            // PREPARE CHANGES
            // =========================================

            var now =
                DateTime.UtcNow;

            // -----------------------------------------
            // TRADER VALUES
            // -----------------------------------------

            var traderEventName =
                trader.EventName ?? "";

            var traderCompanyName =
                trader.CompanyName ?? "";

            var traderOrderId =
                trader.OrderId ?? "";

            var traderPitchSize =
                trader.PitchSize ?? "";

            // -----------------------------------------
            // RELEASE OLD PITCHES
            // -----------------------------------------

            foreach (var pitch in oldPitches)
            {
                pitch.Status = "Available";

                pitch.TraderPassNumber = "";
                pitch.CompanyName = "";
                pitch.OrderId = "";
                pitch.PitchSize = "";
                pitch.SquaresUsed = 1;

                pitch.ReservationId = "";
                pitch.ReservedUntil = null;

                pitch.Updated = now;
            }

            // -----------------------------------------
            // BOOK NEW PITCHES
            // -----------------------------------------

            foreach (var pitch in newPitches)
            {
                pitch.Status = "Booked";

                pitch.EventId =
                    eventId;

                pitch.EventName =
                    traderEventName;

                pitch.TraderPassNumber =
                    traderPassNumber;

                pitch.CompanyName =
                    traderCompanyName;

                pitch.OrderId =
                    traderOrderId;

                pitch.PitchSize =
                    traderPitchSize;

                pitch.SquaresUsed =
                    requiredSquares;

                pitch.ReservationId = "";
                pitch.ReservedUntil = null;

                pitch.Updated = now;
            }

            // =========================================
            // ATOMIC UPDATE
            // =========================================

            try
            {
                await _pitches.ReallocateAsync(
                    oldPitches,
                    newPitches);
            }
            catch (RequestFailedException ex)
                when (ex.Status == 412)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.Conflict,
                    "The pitch allocation changed while this request was being processed. Please refresh the dashboard and try again.");
            }

            // =========================================
            // SUCCESS
            // =========================================

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK);

            await response.WriteAsJsonAsync(
                new
                {
                    success = true,

                    eventId,

                    traderPassNumber =
                        traderPassNumber,

                    companyName =
                        traderCompanyName,

                    customerEmail =
                        trader.CustomerEmail ?? "",

                    oldPitchIds =
                        oldPitches
                            .Select(p => p.PitchId ?? "")
                            .OrderBy(x => x)
                            .ToList(),

                    newPitchIds =
                        newPitches
                            .Select(p => p.PitchId ?? "")
                            .OrderBy(x => x)
                            .ToList(),

                    pitchSize =
                        traderPitchSize,

                    reason =
                        request.Reason ?? ""
                });

            return response;
        }
        catch (Exception ex)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                ex.Message);
        }
    }

    // =============================================
    // VALIDATE PITCH ID
    // =============================================

    private static bool IsValidPitchId(
        string pitchId)
    {
        if (string.IsNullOrWhiteSpace(pitchId) ||
            pitchId.Length < 2)
        {
            return false;
        }

        var row =
            char.ToUpperInvariant(
                pitchId[0]);

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

    // =============================================
    // VALIDATE 8 x 4 PAIR
    // =============================================

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

        // -----------------------------------------
        // NORMAL HORIZONTAL PAIR
        // -----------------------------------------

        if (row1 == row2 &&
            Math.Abs(number1 - number2) == 1)
        {
            return true;
        }

        // -----------------------------------------
        // SPECIAL END-ON PAIRS
        // -----------------------------------------

        return IsSpecialEndPair(
            row1,
            number1,
            row2,
            number2);
    }

    // =============================================
    // SPECIAL END-ON PAIRS
    // =============================================

    private static bool IsSpecialEndPair(
        char row1,
        int number1,
        char row2,
        int number2)
    {
        // A1 + B1
        if (
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
        )
        {
            return true;
        }

        // A10 + B10
        if (
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
        )
        {
            return true;
        }

        // C1 + D1
        if (
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
        )
        {
            return true;
        }

        // C10 + D10
        if (
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
            )
        )
        {
            return true;
        }

        return false;
    }

    // =============================================
    // PARSE PITCH
    // =============================================

    private static bool TryParsePitch(
        string pitchId,
        out char row,
        out int number)
    {
        row = '\0';
        number = 0;

        if (!IsValidPitchId(pitchId))
        {
            return false;
        }

        row =
            char.ToUpperInvariant(
                pitchId[0]);

        return int.TryParse(
            pitchId.Substring(1),
            out number);
    }

    // =============================================
    // ERROR RESPONSE
    // =============================================

    private static async Task<HttpResponseData> ErrorResponse(
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

    // =============================================
    // REQUEST MODEL
    // =============================================

    private class ReallocationRequest
    {
        public string TraderPassNumber { get; set; }
            = "";

        public List<string> NewPitchIds { get; set; }
            = new();

        public string Reason { get; set; }
            = "";
    }
}