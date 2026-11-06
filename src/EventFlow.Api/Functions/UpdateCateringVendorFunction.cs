using System.Net;
using System.Text.Json;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EventFlow.Api.Functions;

public class UpdateCateringVendorFunction
{
    private readonly ICateringVendorRepository _cateringVendors;

    public UpdateCateringVendorFunction(
        ICateringVendorRepository cateringVendors)
    {
        _cateringVendors = cateringVendors;
    }


    // ============================================================
    // UPDATE CATERING VENDOR
    // ============================================================

    [Function("UpdateCateringVendor")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "put",
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
            // GET EXISTING VENDOR
            // ----------------------------------------------------

            var vendor =
                await _cateringVendors
                    .GetCateringVendorAsync(
                        eventId,
                        passNumber
                    );

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
            // READ REQUEST
            // ----------------------------------------------------

            var request =
                await JsonSerializer.DeserializeAsync<
                    UpdateCateringVendorRequest>(
                        req.Body,
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });


            if (request == null)
            {
                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Invalid request."
                );
            }


            // ----------------------------------------------------
            // UPDATE DETAILS
            // ----------------------------------------------------

            if (request.ContactNames != null)
                vendor.ContactNames =
                    request.ContactNames;

            if (request.CompanyName != null)
                vendor.CompanyName =
                    request.CompanyName;

            if (request.CustomerEmail != null)
                vendor.CustomerEmail =
                    request.CustomerEmail;

            if (request.PhoneNumber != null)
                vendor.PhoneNumber =
                    request.PhoneNumber;


            // ----------------------------------------------------
            // ADDRESS
            // ----------------------------------------------------

            if (request.StreetAddress != null)
                vendor.StreetAddress =
                    request.StreetAddress;

            if (request.AddressLine2 != null)
                vendor.AddressLine2 =
                    request.AddressLine2;

            if (request.City != null)
                vendor.City =
                    request.City;

            if (request.CountyRegion != null)
                vendor.CountyRegion =
                    request.CountyRegion;

            if (request.Postcode != null)
                vendor.Postcode =
                    request.Postcode;

            if (request.Country != null)
                vendor.Country =
                    request.Country;


            // ----------------------------------------------------
            // ONLINE DETAILS
            // ----------------------------------------------------

            if (request.FacebookPage != null)
                vendor.FacebookPage =
                    request.FacebookPage;

            if (request.Website != null)
                vendor.Website =
                    request.Website;


            // ----------------------------------------------------
            // FOOD / PITCH
            // ----------------------------------------------------

            if (request.FoodDescription != null)
                vendor.FoodDescription =
                    request.FoodDescription;

            if (request.PitchStyle != null)
                vendor.PitchStyle =
                    request.PitchStyle;

            if (request.UnitWidth.HasValue)
                vendor.UnitWidth =
                    request.UnitWidth.Value;

            if (request.UnitLength.HasValue)
                vendor.UnitLength =
                    request.UnitLength.Value;

            if (request.VehicleRegistration != null)
                vendor.VehicleRegistration =
                    request.VehicleRegistration;

            if (request.NumberOfPeople.HasValue)
                vendor.NumberOfPeople =
                    request.NumberOfPeople.Value;


            // ----------------------------------------------------
            // DOCUMENTS
            // ----------------------------------------------------

            if (request.VehiclePhoto != null)
                vendor.VehiclePhoto =
                    request.VehiclePhoto;

            if (request.PublicLiabilityInsurance != null)
                vendor.PublicLiabilityInsurance =
                    request.PublicLiabilityInsurance;

            if (request.ProductLiabilityInsurance != null)
                vendor.ProductLiabilityInsurance =
                    request.ProductLiabilityInsurance;

            if (request.FoodHygieneCertificates != null)
                vendor.FoodHygieneCertificates =
                    request.FoodHygieneCertificates;

            if (request.GasSafetyCertificate != null)
                vendor.GasSafetyCertificate =
                    request.GasSafetyCertificate;

            if (request.FoodStandardsCertificate != null)
                vendor.FoodStandardsCertificate =
                    request.FoodStandardsCertificate;

            if (request.GasInstallationCertificate != null)
                vendor.GasInstallationCertificate =
                    request.GasInstallationCertificate;

            if (request.Logo != null)
                vendor.Logo =
                    request.Logo;

            if (request.EventMenu != null)
                vendor.EventMenu =
                    request.EventMenu;


            // ----------------------------------------------------
            // TERMS
            // ----------------------------------------------------

            if (request.TermsAccepted.HasValue)
                vendor.TermsAccepted =
                    request.TermsAccepted.Value;

            if (request.SignedBy != null)
                vendor.SignedBy =
                    request.SignedBy;

            if (request.SignedDate.HasValue)
                vendor.SignedDate =
                    request.SignedDate.Value;


            // ----------------------------------------------------
            // SAVE
            // ----------------------------------------------------

            await _cateringVendors
                .UpdateCateringVendorAsync(
                    vendor
                );


            // ----------------------------------------------------
            // RETURN UPDATED VENDOR
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
                "UpdateCateringVendor error: " +
                ex
            );

            return await CreateErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to update catering vendor."
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


// ================================================================
// UPDATE REQUEST
// ================================================================

public class UpdateCateringVendorRequest
{
    public string? ContactNames { get; set; }
    public string? CompanyName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? PhoneNumber { get; set; }

    public string? StreetAddress { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? CountyRegion { get; set; }
    public string? Postcode { get; set; }
    public string? Country { get; set; }

    public string? FacebookPage { get; set; }
    public string? Website { get; set; }

    public string? FoodDescription { get; set; }
    public string? PitchStyle { get; set; }

    public decimal? UnitWidth { get; set; }
    public decimal? UnitLength { get; set; }

    public string? VehicleRegistration { get; set; }
    public int? NumberOfPeople { get; set; }

    public string? VehiclePhoto { get; set; }
    public string? PublicLiabilityInsurance { get; set; }
    public string? ProductLiabilityInsurance { get; set; }
    public string? FoodHygieneCertificates { get; set; }
    public string? GasSafetyCertificate { get; set; }
    public string? FoodStandardsCertificate { get; set; }
    public string? GasInstallationCertificate { get; set; }
    public string? Logo { get; set; }
    public string? EventMenu { get; set; }

    public bool? TermsAccepted { get; set; }
    public string? SignedBy { get; set; }
    public DateTimeOffset? SignedDate { get; set; }
}