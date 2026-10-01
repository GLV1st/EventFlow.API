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
            Route = "traderpitches/{eventId}")]
        HttpRequestData req,
        string eventId)
    {
        var pitches = await _pitches.GetAllAsync(eventId);

        var response = req.CreateResponse(HttpStatusCode.OK);

        await response.WriteAsJsonAsync(
            pitches
                .OrderBy(p => p.PitchId)
                .Select(p => new
                {
                    pitchId = p.PitchId,
                    status = p.Status,
                    companyName = p.CompanyName,
                    traderPassNumber = p.TraderPassNumber,
                    pitchSize = p.PitchSize,
                    squaresUsed = p.SquaresUsed
                }));

        return response;
    }
}