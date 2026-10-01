using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

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
            Route = "traderpitches/release"
        )]
        HttpRequestData req)
    {
        try
        {
            var request =
                await req.ReadFromJsonAsync<ReleaseRequest>();


            if (request == null)
            {
                return await ErrorResponse(
                    req,
                    "Invalid request."
                );
            }


            if (string.IsNullOrWhiteSpace(request.EventId))
            {
                return await ErrorResponse(
                    req,
                    "EventId is required."
                );
            }


            if (string.IsNullOrWhiteSpace(request.ReservationId))
            {
                return await ErrorResponse(
                    req,
                    "ReservationId is required."
                );
            }


            var eventId =
                request.EventId.Trim();


            var reservationId =
                request.ReservationId.Trim();


            /*
             * Get all pitches for this event.
             */
            var pitches =
                await _pitches.GetAllAsync(eventId);


            /*
             * Find only pitches belonging to this reservation.
             *
             * We deliberately only release pitches that are currently
             * Reserved. A Booked pitch can never be released by this
             * endpoint.
             */
            var reservedPitches =
                pitches
                    .Where(p =>
                        string.Equals(
                            p.Status,
                            "Reserved",
                            StringComparison.OrdinalIgnoreCase
                        )
                        &&
                        string.Equals(
                            p.ReservationId,
                            reservationId,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                    .ToList();


            /*
             * Nothing found.
             *
             * This is deliberately treated as success so the customer
             * can still continue selecting a pitch if the reservation
             * has already expired or has already been released.
             */
            if (reservedPitches.Count == 0)
            {
                var nothingResponse =
                    req.CreateResponse(HttpStatusCode.OK);

                await nothingResponse.WriteAsJsonAsync(
                    new
                    {
                        success = true,
                        eventId,
                        reservationId,
                        released = 0,
                        pitchIds = Array.Empty<string>()
                    }
                );

                return nothingResponse;
            }


            var releasedPitchIds =
                new List<string>();


            /*
             * Release each pitch.
             */
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

                pitch.Updated =
                    DateTime.UtcNow;


                await _pitches.UpsertAsync(
                    pitch
                );


                releasedPitchIds.Add(
                    pitch.PitchId
                );
            }


            /*
             * Return the released pitches.
             */
            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );


            await response.WriteAsJsonAsync(
                new
                {
                    success = true,
                    eventId,
                    reservationId,
                    released = releasedPitchIds.Count,
                    pitchIds = releasedPitchIds
                }
            );


            return response;
        }
        catch (Exception ex)
        {
            var response =
                req.CreateResponse(
                    HttpStatusCode.InternalServerError
                );


            await response.WriteAsJsonAsync(
                new
                {
                    success = false,
                    error =
                        "Unable to release the pitch reservation.",
                    detail = ex.Message
                }
            );


            return response;
        }
    }


    private static async Task<HttpResponseData> ErrorResponse(
        HttpRequestData req,
        string message)
    {
        var response =
            req.CreateResponse(
                HttpStatusCode.BadRequest
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


    private class ReleaseRequest
    {
        public string EventId { get; set; } = "";

        public string ReservationId { get; set; } = "";
    }
}