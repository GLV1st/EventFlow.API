using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class GetTraderPitchesFunction
{
    private readonly ITraderPitchRepository _pitches;

    public GetTraderPitchesFunction(
        ITraderPitchRepository pitches)
    {
        _pitches = pitches;
    }


    [Function("GetTraderPitches")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "traderpitches/{eventId}"
        )]
        HttpRequestData req,
        string eventId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return await ErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "EventId is required."
                );
            }


            /*
             * Get all pitches for the event.
             */
            var pitches =
                await _pitches.GetAllAsync(eventId);


            /*
             * Current UTC time.
             */
            var now =
                DateTime.UtcNow;


            /*
             * Check for expired reservations.
             *
             * IMPORTANT:
             *
             * Reserved + future expiry
             *     = still unavailable
             *
             * Reserved + expired
             *     = make available again
             *
             * This means we do NOT need a background
             * process just to clean up expired reservations.
             */
            foreach (var pitch in pitches)
            {
                if (
                    string.Equals(
                        pitch.Status,
                        "Reserved",
                        StringComparison.OrdinalIgnoreCase
                    )
                    &&
                    (
                        !pitch.ReservedUntil.HasValue
                        ||
                        pitch.ReservedUntil.Value <= now
                    )
                )
                {
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

                    pitch.Updated =
                        now;


                    /*
                     * Save the cleaned-up pitch back
                     * to Azure Table Storage.
                     */
                    await _pitches.UpsertAsync(
                        pitch
                    );
                }
            }


            /*
             * Return the current pitch state.
             */
            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );


            await response.WriteAsJsonAsync(
                pitches
                    .OrderBy(
                        p => p.PitchId
                    )
                    .Select(
                        p => new
                        {
                            pitchId =
                                p.PitchId,

                            status =
                                p.Status,

                            companyName =
                                p.CompanyName,

                            traderPassNumber =
                                p.TraderPassNumber,

                            pitchSize =
                                p.PitchSize,

                            squaresUsed =
                                p.SquaresUsed,

                            reservationId =
                                p.ReservationId,

                            reservedUntil =
                                p.ReservedUntil
                        }
                    )
            );


            return response;
        }
        catch (Exception ex)
        {
            return await ErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                ex.Message
            );
        }
    }


    private static async Task<HttpResponseData> ErrorResponse(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message)
    {
        var response =
            req.CreateResponse(
                statusCode
            );


        await response.WriteAsJsonAsync(
            new
            {
                success = false,
                error = message
            }
        );


        return response;
    }
}