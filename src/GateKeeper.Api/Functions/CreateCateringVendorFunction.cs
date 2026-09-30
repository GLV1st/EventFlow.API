using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GateKeeper.Api.Functions;

public class CreateCateringVendorFunction
{
    private readonly ICateringVendorRepository _cateringVendors;
    private readonly IEventRepository _events;


    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public CreateCateringVendorFunction(
        ICateringVendorRepository cateringVendors,
        IEventRepository events)
    {
        _cateringVendors = cateringVendors;
        _events = events;
    }


    // ============================================================
    // CREATE CATERING VENDOR
    // ============================================================

    [Function("CreateCateringVendor")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Function,
            "post",
            Route = "cateringvendors")]
        HttpRequestData req)
    {
        try
        {
            // ----------------------------------------------------
            // READ REQUEST
            // ----------------------------------------------------

            var request =
                await JsonSerializer.DeserializeAsync<
                    CreateCateringVendorRequest>(
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
            // EVENT VALIDATION
            // ----------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                request.EventId))
            {
                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "EventId is required."
                );
            }


            // ----------------------------------------------------
            // CHECK EVENT EXISTS
            // ----------------------------------------------------

            var eventEntity =
                await _events.GetEventAsync(
                    request.EventId
                );


            if (eventEntity == null)
            {
                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.NotFound,
                    "Event '" +
                    request.EventId +
                    "' was not found."
                );
            }


            // ----------------------------------------------------
            // CURRENTLY COP CARNIVAL ONLY
            // ----------------------------------------------------

            if (!string.Equals(
                request.EventId,
                "COP-CARN",
                StringComparison.OrdinalIgnoreCase))
            {
                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "Catering vendors are currently only supported for COP-CARN."
                );
            }


            // ----------------------------------------------------
            // REQUIRED DETAILS
            // ----------------------------------------------------

            if (string.IsNullOrWhiteSpace(
                request.CompanyName))
            {
                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "CompanyName is required."
                );
            }


            if (string.IsNullOrWhiteSpace(
                request.CustomerEmail))
            {
                return await CreateErrorResponse(
                    req,
                    HttpStatusCode.BadRequest,
                    "CustomerEmail is required."
                );
            }


            // ----------------------------------------------------
            // RESERVE SHARED TRADER NUMBER
            // ----------------------------------------------------
            //
            // Catering uses the SAME numbering sequence as
            // existing Traders.
            //
            // ReserveTraderNumberAsync returns an integer.
            //
            // Example:
            //
            // 1  -> COP-TRADER-0001
            // 2  -> COP-TRADER-0002
            // 3  -> COP-TRADER-0003
            //
            // ----------------------------------------------------

            var traderNumber =
                await _events.ReserveTraderNumberAsync(
                    request.EventId
                );


            var passNumber =
                "COP-TRADER-" +
                traderNumber.ToString("D4");


            // ----------------------------------------------------
            // GENERATE SECURE DOCUMENT UPLOAD TOKEN
            // ----------------------------------------------------
            //
            // This token is randomly generated and is not based
            // on the catering pass number or customer details.
            //
            // The token will be used later in the secure
            // document-upload link.
            //
            // ----------------------------------------------------

            var documentUploadToken =
                Convert.ToHexString(
                    RandomNumberGenerator.GetBytes(32)
                ).ToLowerInvariant();


            // ----------------------------------------------------
            // TOKEN EXPIRY
            // ----------------------------------------------------
            //
            // Upload links remain valid for 365 days.
            //
            // We can change this later if required.
            //
            // ----------------------------------------------------

            var documentUploadTokenExpiresAt =
                DateTimeOffset.UtcNow.AddDays(365);


            // ----------------------------------------------------
            // CREATE ENTITY
            // ----------------------------------------------------

            var cateringVendor =
                new CateringVendorEntity
                {
                    // ------------------------------------------------
                    // TABLE KEYS
                    // ------------------------------------------------

                    PartitionKey =
                        request.EventId,

                    RowKey =
                        passNumber,


                    // ------------------------------------------------
                    // GATEKEEPER
                    // ------------------------------------------------

                    EventId =
                        request.EventId,

                    CateringPassNumber =
                        passNumber,

                    OrderId =
                        request.OrderId ?? string.Empty,

                    OrderItemId =
                        request.OrderItemId ?? string.Empty,


                    // ------------------------------------------------
                    // CONTACT
                    // ------------------------------------------------

                    ContactNames =
                        request.ContactNames ?? string.Empty,

                    CompanyName =
                        request.CompanyName ?? string.Empty,

                    CustomerEmail =
                        request.CustomerEmail ?? string.Empty,

                    PhoneNumber =
                        request.PhoneNumber ?? string.Empty,


                    // ------------------------------------------------
                    // ADDRESS
                    // ------------------------------------------------

                    StreetAddress =
                        request.StreetAddress ?? string.Empty,

                    AddressLine2 =
                        request.AddressLine2 ?? string.Empty,

                    City =
                        request.City ?? string.Empty,

                    CountyRegion =
                        request.CountyRegion ?? string.Empty,

                    Postcode =
                        request.Postcode ?? string.Empty,

                    Country =
                        request.Country ?? string.Empty,


                    // ------------------------------------------------
                    // ONLINE
                    // ------------------------------------------------

                    FacebookPage =
                        request.FacebookPage ?? string.Empty,

                    Website =
                        request.Website ?? string.Empty,


                    // ------------------------------------------------
                    // FOOD / PITCH
                    // ------------------------------------------------

                    FoodDescription =
                        request.FoodDescription ?? string.Empty,

                    PitchStyle =
                        request.PitchStyle ?? string.Empty,

                    UnitWidth =
                        request.UnitWidth,

                    UnitLength =
                        request.UnitLength,

                    VehicleRegistration =
                        request.VehicleRegistration ?? string.Empty,

                    NumberOfPeople =
                        request.NumberOfPeople,


                    // ------------------------------------------------
                    // DOCUMENTS
                    // ------------------------------------------------
                    //
                    // These remain empty initially.
                    //
                    // They will be populated with Azure Blob paths
                    // when the vendor uploads their documents.
                    //
                    // ------------------------------------------------

                    VehiclePhoto =
                        request.VehiclePhoto ?? string.Empty,

                    PublicLiabilityInsurance =
                        request.PublicLiabilityInsurance ?? string.Empty,

                    ProductLiabilityInsurance =
                        request.ProductLiabilityInsurance ?? string.Empty,

                    FoodHygieneCertificates =
                        request.FoodHygieneCertificates ?? string.Empty,

                    GasSafetyCertificate =
                        request.GasSafetyCertificate ?? string.Empty,

                    FoodStandardsCertificate =
                        request.FoodStandardsCertificate ?? string.Empty,

                    GasInstallationCertificate =
                        request.GasInstallationCertificate ?? string.Empty,

                    Logo =
                        request.Logo ?? string.Empty,

                    EventMenu =
                        request.EventMenu ?? string.Empty,


                    // ------------------------------------------------
                    // DOCUMENT UPLOAD SECURITY
                    // ------------------------------------------------

                    DocumentUploadToken =
                        documentUploadToken,

                    DocumentUploadTokenExpiresAt =
                        documentUploadTokenExpiresAt,


                    // ------------------------------------------------
                    // TERMS
                    // ------------------------------------------------

                    TermsAccepted =
                        request.TermsAccepted,

                    SignedBy =
                        request.SignedBy ?? string.Empty,

                    SignedDate =
                        request.SignedDate,


                    // ------------------------------------------------
                    // SCANNING
                    // ------------------------------------------------

                    CheckedIn =
                        false,

                    CheckedInAt =
                        null,

                    CheckedOut =
                        false,

                    CheckedOutAt =
                        null
                };


            // ----------------------------------------------------
            // SAVE TO AZURE TABLE
            // ----------------------------------------------------

            await _cateringVendors.AddCateringVendorAsync(
                cateringVendor
            );


            // ----------------------------------------------------
            // RETURN CREATED RECORD
            // ----------------------------------------------------
            //
            // The upload token is returned here because this
            // endpoint is called by our trusted WooCommerce
            // processing code using the Azure Function key.
            //
            // The normal GET vendor endpoint does NOT expose
            // this token.
            //
            // ----------------------------------------------------

            var response =
                req.CreateResponse(
                    HttpStatusCode.Created
                );


            await response.WriteAsJsonAsync(
                new
                {
                    cateringVendor.EventId,
                    cateringVendor.CateringPassNumber,
                    cateringVendor.OrderId,
                    cateringVendor.OrderItemId,

                    cateringVendor.CompanyName,
                    cateringVendor.CustomerEmail,

                    DocumentUploadToken =
                        cateringVendor.DocumentUploadToken,

                    DocumentUploadTokenExpiresAt =
                        cateringVendor.DocumentUploadTokenExpiresAt
                }
            );


            return response;
        }
        catch (Exception ex)
        {
            // ----------------------------------------------------
            // LOG ERROR
            // ----------------------------------------------------

            Console.Error.WriteLine(
                "CreateCateringVendor error: " +
                ex
            );


            return await CreateErrorResponse(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to create catering vendor."
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
// REQUEST MODEL
// ================================================================

public class CreateCateringVendorRequest
{
    // ============================================================
    // GATEKEEPER
    // ============================================================

    public string EventId { get; set; } = string.Empty;

    public string? OrderId { get; set; }

    public string? OrderItemId { get; set; }


    // ============================================================
    // CONTACT
    // ============================================================

    public string? ContactNames { get; set; }

    public string? CompanyName { get; set; }

    public string? CustomerEmail { get; set; }

    public string? PhoneNumber { get; set; }


    // ============================================================
    // ADDRESS
    // ============================================================

    public string? StreetAddress { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? CountyRegion { get; set; }

    public string? Postcode { get; set; }

    public string? Country { get; set; }


    // ============================================================
    // ONLINE
    // ============================================================

    public string? FacebookPage { get; set; }

    public string? Website { get; set; }


    // ============================================================
    // FOOD / PITCH
    // ============================================================

    public string? FoodDescription { get; set; }

    public string? PitchStyle { get; set; }

    public decimal UnitWidth { get; set; }

    public decimal UnitLength { get; set; }

    public string? VehicleRegistration { get; set; }

    public int NumberOfPeople { get; set; }


    // ============================================================
    // DOCUMENTS
    // ============================================================

    public string? VehiclePhoto { get; set; }

    public string? PublicLiabilityInsurance { get; set; }

    public string? ProductLiabilityInsurance { get; set; }

    public string? FoodHygieneCertificates { get; set; }

    public string? GasSafetyCertificate { get; set; }

    public string? FoodStandardsCertificate { get; set; }

    public string? GasInstallationCertificate { get; set; }

    public string? Logo { get; set; }

    public string? EventMenu { get; set; }


    // ============================================================
    // TERMS
    // ============================================================

    public bool TermsAccepted { get; set; }

    public string? SignedBy { get; set; }

    public DateTimeOffset? SignedDate { get; set; }
}