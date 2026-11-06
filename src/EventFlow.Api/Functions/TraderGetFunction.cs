using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace EventFlow.Api.Functions;

public class TraderGetFunction
{
    private readonly ITraderRepository _traders;

    public TraderGetFunction(ITraderRepository traders)
    {
        _traders = traders;
    }

    [Function("TraderGet")]
    public async Task<HttpResponseData> GetByEvent(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "traders/{eventId}")]
        HttpRequestData req,
        string eventId)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eventId))
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var traders = await _traders.GetTradersByEventAsync(eventId);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(traders);
            return response;
        }
        catch (Exception ex)
        {
            Console.WriteLine("TRADER GET ERROR");
            Console.WriteLine(ex);

            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync("Unable to load traders.");
            return response;
        }
    }
}
