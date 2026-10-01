using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace GateKeeper.Api.Functions;

public class InitializeTraderPitchesFunction
{
    private readonly ITraderPitchRepository _pitches;

    public InitializeTraderPitchesFunction(
        ITraderPitchRepository pitches)
    {
        _pitches = pitches;
    }

    [Function("InitializeTraderPitches")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "traderpitches/initialize/{eventId}")]
        HttpRequestData req,
        string eventId)
    {
        var rows = new[]
        {
            'A',
            'B',
            'C',
            'D',
            'E'
        };

        var created = 0;

        foreach (var row in rows)
        {
            for (var number = 1; number <= 10; number++)
            {
                var pitchId =
                    $"{row}{number}";

                var existing =
                    await _pitches.GetAsync(
                        eventId,
                        pitchId);

                if (existing != null)
                {
                    continue;
                }

                var pitch =
                    new TraderPitchEntity
                    {
                        PartitionKey = eventId,
                        RowKey = pitchId,

                        PitchId = pitchId,

                        Status = "Available",

                        EventId = eventId,

                        EventName = "COP Carnival",

                        TraderPassNumber = "",
                        CompanyName = "",
                        OrderId = "",

                        PitchSize = "",

                        SquaresUsed = 1,

                        ReservationId = "",
                        ReservedUntil = null,

                        Created = DateTime.UtcNow,
                        Updated = DateTime.UtcNow
                    };

                await _pitches.UpsertAsync(
                    pitch);

                created++;
            }
        }

        var response =
            req.CreateResponse(
                HttpStatusCode.OK);

        await response.WriteAsJsonAsync(
            new
            {
                success = true,
                eventId,
                created,
                totalPitches = 50
            });

        return response;
    }
}