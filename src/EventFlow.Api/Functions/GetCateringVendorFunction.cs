using System.Net;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EventFlow.Api.Functions;

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
            // RETURN SAFE VENDOR DATA
            // ----------------------------------------------------
            //
            // IMPORTANT:
            // Do NOT return DocumentUploadToken.
            //
            // The token is effectively the password for the
            // document upload page and must never be exposed
            // through the normal vendor API.
            //
            // ----------------------------------------------------

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );

            await response.WriteAsJsonAsync(
                new
                {
                    vendor.EventId,
                    vendor.CateringPassNumber,
                    vendor.OrderId,
                    vendor.OrderItemId,

                    vendor.ContactNames,
                    vendor.CompanyName,
                    vendor.CustomerEmail,
                    vendor.PhoneNumber,

                    vendor.StreetAddress,
                    vendor.AddressLine2,
                    vendor.City,
                    vendor.CountyRegion,
                    vendor.Postcode,
                    vendor.Country,

                    vendor.FacebookPage,
                    vendor.Website,
                    vendor.FoodDescription,
                    vendor.PitchStyle,

                    vendor.UnitWidth,
                    vendor.UnitLength,

                    vendor.VehicleRegistration,
                    vendor.NumberOfPeople,

                    // ------------------------------------------------
                    // DOCUMENT STATUS
                    // ------------------------------------------------
                    //
                    // We expose whether something has been uploaded,
                    // but NOT the secure upload token.
                    //
                    Documents = new
                    {
                        VehiclePhoto =
                            !string.IsNullOrWhiteSpace(
                                vendor.VehiclePhoto
                            ),

                        PublicLiabilityInsurance =
                            !string.IsNullOrWhiteSpace(
                                vendor.PublicLiabilityInsurance
                            ),

                        ProductLiabilityInsurance =
                            !string.IsNullOrWhiteSpace(
                                vendor.ProductLiabilityInsurance
                            ),

                        FoodHygieneCertificates =
                            !string.IsNullOrWhiteSpace(
                                vendor.FoodHygieneCertificates
                            ),

                        GasSafetyCertificate =
                            !string.IsNullOrWhiteSpace(
                                vendor.GasSafetyCertificate
                            ),

                        FoodStandardsCertificate =
                            !string.IsNullOrWhiteSpace(
                                vendor.FoodStandardsCertificate
                            ),

                        GasInstallationCertificate =
                            !string.IsNullOrWhiteSpace(
                                vendor.GasInstallationCertificate
                            ),

                        EventMenu =
                            !string.IsNullOrWhiteSpace(
                                vendor.EventMenu
                            ),

                        Logo =
                            !string.IsNullOrWhiteSpace(
                                vendor.Logo
                            )
                    },

                    vendor.TermsAccepted,
                    vendor.SignedBy,
                    vendor.SignedDate,

                    vendor.CheckedIn,
                    vendor.CheckedInAt,

                    vendor.CheckedOut,
                    vendor.CheckedOutAt
                }
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