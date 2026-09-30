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
            // BUILD SAFE DASHBOARD RESPONSE
            // ----------------------------------------------------

            var results =
                vendors.Select(vendor => new
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

                    vendor.TermsAccepted,
                    vendor.SignedBy,
                    vendor.SignedDate,

                    // ------------------------------------------------
                    // DOCUMENT STATUS
                    // ------------------------------------------------

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
                    }
                })
                .ToList();


            // ----------------------------------------------------
            // RETURN RESULTS
            // ----------------------------------------------------

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );

            await response.WriteAsJsonAsync(
                results
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