using System.Net;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GateKeeper.Api.Functions;

public class GetCateringDocumentStatusFunction
{
    private readonly ICateringVendorRepository _cateringVendors;

    public GetCateringDocumentStatusFunction(
        ICateringVendorRepository cateringVendors)
    {
        _cateringVendors = cateringVendors;
    }


    // ============================================================
    // GET DOCUMENT STATUS
    // ============================================================

    [Function("GetCateringDocumentStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "cateringdocuments/status/{eventId}/{passNumber}")]
        HttpRequestData req,
        string eventId,
        string passNumber)
    {
        try
        {
            // ----------------------------------------------------
            // READ TOKEN
            // ----------------------------------------------------

            var query =
                System.Web.HttpUtility.ParseQueryString(
                    req.Url.Query
                );

            var token =
                query["token"];


            // ----------------------------------------------------
            // VALIDATE
            // ----------------------------------------------------

            if (string.IsNullOrWhiteSpace(eventId))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "EventId is required."
                );
            }

            if (string.IsNullOrWhiteSpace(passNumber))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "PassNumber is required."
                );
            }

            if (string.IsNullOrWhiteSpace(token))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "Upload token is required."
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


            if (vendor == null)
            {
                return await Error(
                    req,
                    HttpStatusCode.NotFound,
                    "Catering vendor not found."
                );
            }


            // ----------------------------------------------------
            // VALIDATE TOKEN
            // ----------------------------------------------------

            if (!string.Equals(
                vendor.DocumentUploadToken,
                token,
                StringComparison.Ordinal))
            {
                return await Error(
                    req,
                    HttpStatusCode.Unauthorized,
                    "Invalid upload link."
                );
            }


            // ----------------------------------------------------
            // CHECK TOKEN EXPIRY
            // ----------------------------------------------------

            if (vendor.DocumentUploadTokenExpiresAt.HasValue &&
                vendor.DocumentUploadTokenExpiresAt.Value <
                DateTimeOffset.UtcNow)
            {
                return await Error(
                    req,
                    HttpStatusCode.Unauthorized,
                    "This upload link has expired."
                );
            }


            // ----------------------------------------------------
            // RETURN SAFE STATUS
            // ----------------------------------------------------
            //
            // We deliberately return TRUE/FALSE only.
            //
            // The Blob paths and upload token are NOT returned.
            //
            // ----------------------------------------------------

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );


            await response.WriteAsJsonAsync(
                new
                {
                    success = true,

                    eventId =
                        vendor.EventId,

                    cateringPassNumber =
                        vendor.CateringPassNumber,

                    companyName =
                        vendor.CompanyName,

                    documents = new
                    {
                        vehiclePhoto =
                            !string.IsNullOrWhiteSpace(
                                vendor.VehiclePhoto
                            ),

                        publicLiabilityInsurance =
                            !string.IsNullOrWhiteSpace(
                                vendor.PublicLiabilityInsurance
                            ),

                        productLiabilityInsurance =
                            !string.IsNullOrWhiteSpace(
                                vendor.ProductLiabilityInsurance
                            ),

                        foodHygieneCertificates =
                            !string.IsNullOrWhiteSpace(
                                vendor.FoodHygieneCertificates
                            ),

                        gasSafetyCertificate =
                            !string.IsNullOrWhiteSpace(
                                vendor.GasSafetyCertificate
                            ),

                        foodStandardsCertificate =
                            !string.IsNullOrWhiteSpace(
                                vendor.FoodStandardsCertificate
                            ),

                        gasInstallationCertificate =
                            !string.IsNullOrWhiteSpace(
                                vendor.GasInstallationCertificate
                            ),

                        eventMenu =
                            !string.IsNullOrWhiteSpace(
                                vendor.EventMenu
                            ),

                        logo =
                            !string.IsNullOrWhiteSpace(
                                vendor.Logo
                            )
                    }
                }
            );


            return response;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "GetCateringDocumentStatus error: " +
                ex
            );


            return await Error(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to retrieve document status."
            );
        }
    }


    // ============================================================
    // ERROR RESPONSE
    // ============================================================

    private static async Task<HttpResponseData> Error(
        HttpRequestData req,
        HttpStatusCode status,
        string message)
    {
        var response =
            req.CreateResponse(
                status
            );


        await response.WriteAsJsonAsync(
            new
            {
                error = message
            }
        );


        return response;
    }
}