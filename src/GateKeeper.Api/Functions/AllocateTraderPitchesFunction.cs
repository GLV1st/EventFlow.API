using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using System.Text.Json;

namespace GateKeeper.Api.Functions;

public class AllocateTraderPitchesFunction
{
    private readonly ITraderPitchRepository _pitches;

    public AllocateTraderPitchesFunction(
        ITraderPitchRepository pitches)
    {
        _pitches = pitches;
    }

    [Function("AllocateTraderPitches")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "traderpitches/allocate/{eventId}")]
        HttpRequestData req,
        string eventId)
    {
        try
        {
            var request =
                await JsonSerializer.DeserializeAsync<AllocationRequest>(
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


            if (string.IsNullOrWhiteSpace(
                    request.CompanyName))
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "CompanyName is required.");
            }


            if (request.PitchIds == null ||
                request.PitchIds.Count == 0)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "At least one PitchId is required.");
            }


            // -----------------------------------------
            // NORMALISE PITCH SIZE
            // -----------------------------------------

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
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "PitchSize must be 4x4 or 8x4.");
            }


            // -----------------------------------------
            // CHECK NUMBER OF PITCHES
            // -----------------------------------------

            if (request.PitchIds.Count != requiredSquares)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    $"A {request.PitchSize} pitch requires {requiredSquares} space(s).");
            }


            // -----------------------------------------
            // REMOVE DUPLICATES
            // -----------------------------------------

            var pitchIds =
                request.PitchIds
                    .Select(x =>
                        (x ?? "")
                        .Trim()
                        .ToUpperInvariant())
                    .ToList();

            if (pitchIds.Count !=
                pitchIds.Distinct().Count())
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Duplicate pitch IDs were supplied.");
            }


            // -----------------------------------------
            // VALIDATE PITCH IDs
            // -----------------------------------------

            foreach (var pitchId in pitchIds)
            {
                if (!IsValidPitchId(pitchId))
                {
                    return await ErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        $"Invalid pitch ID: {pitchId}");
                }
            }


            // -----------------------------------------
            // VALIDATE 8x4 COMBINATION
            // -----------------------------------------

            if (requiredSquares == 2)
            {
                if (!IsValidEightByFourPair(
                        pitchIds[0],
                        pitchIds[1]))
                {
                    return await ErrorResponse(
                        req,
                        HttpStatusCode.BadRequest,
                        "The selected spaces are not a valid 8m x 4m pitch combination.");
                }
            }


            // -----------------------------------------
            // LOAD SELECTED PITCHES
            // -----------------------------------------

            var pitches =
                new List<GateKeeper.Storage.Entities.TraderPitchEntity>();

            foreach (var pitchId in pitchIds)
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

                pitches.Add(pitch);
            }


            // -----------------------------------------
            // CHECK EXISTING ALLOCATION
            // -----------------------------------------
            //
            // This makes a repeated request safe.
            //
            // If the same trader already owns
            // exactly these pitches, return success
            // instead of trying to allocate them again.
            //
            // -----------------------------------------

            var alreadyAllocated =
                pitches.All(p =>
                    p.Status == "Booked" &&
                    string.Equals(
                        p.TraderPassNumber,
                        request.TraderPassNumber,
                        StringComparison.OrdinalIgnoreCase));

            if (alreadyAllocated)
            {
                var existingResponse =
                    req.CreateResponse(
                        HttpStatusCode.OK);

                await existingResponse.WriteAsJsonAsync(
                    new
                    {
                        success = true,
                        alreadyAllocated = true,
                        eventId,
                        traderPassNumber =
                            request.TraderPassNumber,
                        pitchIds
                    });

                return existingResponse;
            }


            // -----------------------------------------
            // CHECK AVAILABILITY
            // -----------------------------------------

            var unavailable =
                pitches.FirstOrDefault(
                    p => p.Status != "Available");

            if (unavailable != null)
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.Conflict,
                    $"Pitch {unavailable.PitchId} is no longer available.");
            }


            // -----------------------------------------
            // PREPARE ALLOCATION
            // -----------------------------------------

            var now =
                DateTime.UtcNow;

            foreach (var pitch in pitches)
            {
                pitch.Status = "Booked";
                pitch.EventId = eventId;
                pitch.TraderPassNumber =
                    request.TraderPassNumber;
                pitch.CompanyName =
                    request.CompanyName;
                pitch.OrderId =
                    request.OrderId ?? "";
                pitch.PitchSize =
                    request.PitchSize ?? "";
                pitch.SquaresUsed =
                    requiredSquares;
                pitch.Updated =
                    now;
            }


            // -----------------------------------------
            // ATOMIC AZURE TRANSACTION
            // -----------------------------------------

            try
            {
                await _pitches.AllocateAsync(
                    pitches);
            }
            catch (
                Azure.RequestFailedException ex)
                when (ex.Status == 412)
            {
                // Another trader changed one of the
                // selected pitches between our read
                // and our transaction.

                return await ErrorResponse(
                    req,
                    HttpStatusCode.Conflict,
                    "One or more of the selected pitches were booked by another trader. Please choose another location.");
            }


            // -----------------------------------------
            // SUCCESS
            // -----------------------------------------

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK);

            await response.WriteAsJsonAsync(
                new
                {
                    success = true,
                    alreadyAllocated = false,
                    eventId,
                    traderPassNumber =
                        request.TraderPassNumber,
                    companyName =
                        request.CompanyName,
                    orderId =
                        request.OrderId,
                    pitchSize =
                        request.PitchSize,
                    pitchIds
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
        if (string.IsNullOrWhiteSpace(pitchId))
            return false;

        if (pitchId.Length < 2)
            return false;

        var row =
            pitchId[0];

        if (row < 'A' ||
            row > 'F')
            return false;

        if (!int.TryParse(
                pitchId.Substring(1),
                out var number))
            return false;

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
            return false;

        if (!TryParsePitch(
                second,
                out var row2,
                out var number2))
            return false;


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

        if (IsSpecialEndPair(
                row1,
                number1,
                row2,
                number2))
        {
            return true;
        }

        return false;
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
        return
            (
                row1 == 'B' &&
                number1 == 1 &&
                row2 == 'C' &&
                number2 == 1
            )
            ||
            (
                row1 == 'C' &&
                number1 == 1 &&
                row2 == 'B' &&
                number2 == 1
            )
            ||
            (
                row1 == 'B' &&
                number1 == 10 &&
                row2 == 'C' &&
                number2 == 10
            )
            ||
            (
                row1 == 'C' &&
                number1 == 10 &&
                row2 == 'B' &&
                number2 == 10
            )
            ||
            (
                row1 == 'D' &&
                number1 == 1 &&
                row2 == 'E' &&
                number2 == 1
            )
            ||
            (
                row1 == 'E' &&
                number1 == 1 &&
                row2 == 'D' &&
                number2 == 1
            )
            ||
            (
                row1 == 'D' &&
                number1 == 10 &&
                row2 == 'E' &&
                number2 == 10
            )
            ||
            (
                row1 == 'E' &&
                number1 == 10 &&
                row2 == 'D' &&
                number2 == 10
            );
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
            return false;

        row =
            pitchId[0];

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

    private class AllocationRequest
    {
        public string TraderPassNumber { get; set; }
            = "";

        public string CompanyName { get; set; }
            = "";

        public string? OrderId { get; set; }

        public string PitchSize { get; set; }
            = "";

        public List<string> PitchIds { get; set; }
            = new();
    }
}