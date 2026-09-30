using System.Net;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GateKeeper.Api.Functions;

public class GetCateringVendorsFunction
{
    private readonly ICateringVendorRepository _cateringVendors;

    public GetCateringVendorsFunction(
        ICateringVendorRepository cateringVendors)
    {
        _cateringVendors = cateringVendors;
    }


    // ============================================================
    // GET ALL CATERING VENDORS FOR AN EVENT
    // ============================================================

    [Function("GetCateringVendors")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "cateringvendors/{eventId}")]
        HttpRequestData req,
        string eventId)
    {
        try
        {
            // ----------------------------------------------------
            // VALIDATE EVENT
            // ----------------------------------------------------

            if (string.IsNullOrWhiteSpace(eventId))
            {
                var badResponse =
                    req.CreateResponse(
                        HttpStatusCode.BadRequest
                    );

                await badResponse.WriteAsJsonAsync(
                    new
                    {
                        error = "EventId is required."
                    }
                );

                return badResponse;
            }


            // ----------------------------------------------------
            // GET VENDORS
            // ----------------------------------------------------

            var vendors =
                await _cateringVendors
                    .GetCateringVendorsByEventAsync(
                        eventId
                    );


            // ----------------------------------------------------
            // RETURN RESULTS
            // ----------------------------------------------------

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );

            await response.WriteAsJsonAsync(
                vendors
            );

            return response;
        }
        catch (Exception ex)
        {
            // ----------------------------------------------------
            // LOG ERROR
            // ----------------------------------------------------

            Console.Error.WriteLine(
                "GetCateringVendors error: " +
                ex
            );


            var response =
                req.CreateResponse(
                    HttpStatusCode.InternalServerError
                );

            await response.WriteAsJsonAsync(
                new
                {
                    error =
                        "Unable to retrieve catering vendors."
                }
            );

            return response;
        }
    }
}