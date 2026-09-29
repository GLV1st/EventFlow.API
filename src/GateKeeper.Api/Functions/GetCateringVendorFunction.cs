using System.Net;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GateKeeper.Api.Functions;

public class GetCateringVendorFunction
{
    private readonly ICateringVendorRepository _cateringVendors;

    public GetCateringVendorFunction(
        ICateringVendorRepository cateringVendors)
    {
        _cateringVendors = cateringVendors;
    }


    // ============================================================
    // GET ONE CATERING VENDOR
    // ============================================================

    [Function("GetCateringVendor")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "get",
            Route = "cateringvendors/{eventId}/{passNumber}")]
        HttpRequestData req,
        string eventId,
        string passNumber)
    {
        try
        {
            // ----------------------------------------------------
            // VALIDATE
            // ----------------------------------------------------

            if (string.IsNullOrWhiteSpace(eventId))
            {
                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "EventId is required."
                );
            }

            if (string.IsNullOrWhiteSpace(passNumber))
            {
                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "PassNumber is required."
                );
            }


            // ----------------------------------------------------
            // GET VENDOR
            // ----------------------------------------------------

            var vendor =
                await _cateringVendors
                    .GetCateringVendorAsync(
                        eventId,
                        passNumber
                    );


            // ----------------------------------------------------
            // NOT FOUND
            // ----------------------------------------------------

            if (vendor == null)
            {
                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.NotFound,
                    "Catering vendor '" +
                    passNumber +
                    "' was not found."
                );
            }


            // ----------------------------------------------------
            // RETURN VENDOR
            // ----------------------------------------------------

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );

            await response.WriteAsJsonAsync(
                vendor
            );

            return response;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "GetCateringVendor error: " +
                ex
            );

            return await CreateErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to retrieve catering vendor."
            );
        }
    }


    // ============================================================
    // ERROR RESPONSE
    // ============================================================

    private static async Task<HttpResponseData>
        CreateErrorResponse(
            HttpRequestData req,
            HttpStatusCode status,
            string message)
    {
        var response =
            req.CreateResponse(status);

        await response.WriteAsJsonAsync(
            new
            {
                error = message
            }
        );

        return response;
    }
}