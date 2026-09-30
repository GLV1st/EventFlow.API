using System.Net;
using Azure.Storage.Blobs;
using GateKeeper.Storage.Entities;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Api.Functions;

public class CateringDocumentUploadFunction
{
    private readonly ICateringVendorRepository _cateringVendors;
    private readonly IConfiguration _configuration;

    private const long MaxFileSize =
        20 * 1024 * 1024; // 20 MB

    private const string ContainerName =
        "catering-documents";


    // ============================================================
    // ALLOWED DOCUMENT TYPES
    // ============================================================

    private static readonly Dictionary<string, string[]>
        AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["vehicle-photo"] =
                    new[]
                    {
                        ".jpg",
                        ".jpeg",
                        ".png",
                        ".webp"
                    },

                ["public-liability"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    },

                ["product-liability"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    },

                ["food-hygiene"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    },

                ["gas-safety"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    },

                ["food-standards"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    },

                ["gas-installation"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    },

                ["event-menu"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    },

                ["logo"] =
                    new[]
                    {
                        ".jpg",
                        ".jpeg",
                        ".png",
                        ".webp"
                    }
            };


    // ============================================================
    // CONSTRUCTOR
    // ============================================================

    public CateringDocumentUploadFunction(
        ICateringVendorRepository cateringVendors,
        IConfiguration configuration)
    {
        _cateringVendors =
            cateringVendors;

        _configuration =
            configuration;
    }


    // ============================================================
    // UPLOAD DOCUMENT
    // ============================================================

    [Function("UploadCateringDocument")]
    public async Task<HttpResponseData> Upload(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "cateringdocuments/upload")]
        HttpRequestData req)
    {
        try
        {
            // ----------------------------------------------------
            // READ QUERY STRING
            // ----------------------------------------------------

            var query =
                System.Web.HttpUtility.ParseQueryString(
                    req.Url.Query
                );

            var eventId =
                query["eventId"];

            var passNumber =
                query["passNumber"];

            var token =
                query["token"];

            var documentType =
                query["documentType"];


            // ----------------------------------------------------
            // VALIDATE REQUEST
            // ----------------------------------------------------

            if (string.IsNullOrWhiteSpace(eventId) ||
                string.IsNullOrWhiteSpace(passNumber) ||
                string.IsNullOrWhiteSpace(token) ||
                string.IsNullOrWhiteSpace(documentType))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "Missing upload information."
                );
            }


            // ----------------------------------------------------
            // VALIDATE DOCUMENT TYPE
            // ----------------------------------------------------

            if (!AllowedExtensions.ContainsKey(
                documentType))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "Invalid document type."
                );
            }


            // ----------------------------------------------------
            // GET CATERING VENDOR
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
            // GET FILE NAME
            // ----------------------------------------------------

            string? fileName = null;

            if (req.Headers.TryGetValues(
                "X-File-Name",
                out var fileNameValues))
            {
                fileName =
                    fileNameValues.FirstOrDefault();
            }


            if (string.IsNullOrWhiteSpace(fileName))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "File name is required."
                );
            }


            // ----------------------------------------------------
            // CLEAN FILE NAME
            // ----------------------------------------------------

            fileName =
                Path.GetFileName(fileName);

            var extension =
                Path.GetExtension(fileName);


            // ----------------------------------------------------
            // CHECK EXTENSION
            // ----------------------------------------------------

            if (string.IsNullOrWhiteSpace(extension) ||
                !AllowedExtensions[documentType]
                    .Contains(
                        extension,
                        StringComparer.OrdinalIgnoreCase))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "This file type is not allowed for this document."
                );
            }


            // ----------------------------------------------------
            // READ FILE
            // ----------------------------------------------------

            using var memoryStream =
                new MemoryStream();

            await req.Body.CopyToAsync(
                memoryStream
            );


            // ----------------------------------------------------
            // CHECK FILE SIZE
            // ----------------------------------------------------

            if (memoryStream.Length == 0)
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "The uploaded file is empty."
                );
            }


            if (memoryStream.Length > MaxFileSize)
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "The maximum file size is 20 MB."
                );
            }


            memoryStream.Position = 0;


            // ----------------------------------------------------
            // GET STORAGE CONNECTION
            // ----------------------------------------------------

            var connectionString =
                _configuration["StorageConnection"];


            if (string.IsNullOrWhiteSpace(
                connectionString))
            {
                throw new InvalidOperationException(
                    "StorageConnection configuration is not configured."
                );
            }


            // ----------------------------------------------------
            // CREATE BLOB SERVICE
            // ----------------------------------------------------

            var blobService =
                new BlobServiceClient(
                    connectionString
                );


            // ----------------------------------------------------
            // GET CONTAINER
            // ----------------------------------------------------

            var container =
                blobService.GetBlobContainerClient(
                    ContainerName
                );


            await container.CreateIfNotExistsAsync();


            // ----------------------------------------------------
            // CREATE SAFE BLOB PATH
            // ----------------------------------------------------

            var safeEventId =
                CleanPathPart(eventId);

            var safePassNumber =
                CleanPathPart(passNumber);

            var safeDocumentType =
                CleanPathPart(documentType);


            var blobName =
                $"{safeEventId}/" +
                $"{safePassNumber}/" +
                $"{safeDocumentType}" +
                extension.ToLowerInvariant();


            // ----------------------------------------------------
            // UPLOAD TO AZURE BLOB STORAGE
            // ----------------------------------------------------

            var blob =
                container.GetBlobClient(
                    blobName
                );


            await blob.UploadAsync(
                memoryStream,
                overwrite: true
            );


            // ----------------------------------------------------
            // UPDATE CATERING VENDOR
            // ----------------------------------------------------

            SetDocumentPath(
                vendor,
                documentType,
                blobName
            );


            await _cateringVendors
                .UpdateCateringVendorAsync(
                    vendor
                );


            // ----------------------------------------------------
            // SUCCESS
            // ----------------------------------------------------

            var response =
                req.CreateResponse(
                    HttpStatusCode.OK
                );


            await response.WriteAsJsonAsync(
                new
                {
                    success = true,
                    documentType,
                    fileName,
                    blobPath = blobName
                }
            );


            return response;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "UploadCateringDocument error: " +
                ex
            );


            return await Error(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to upload document."
            );
        }
    }


    // ============================================================
    // SET DOCUMENT PATH
    // ============================================================

    private static void SetDocumentPath(
        CateringVendorEntity vendor,
        string documentType,
        string blobPath)
    {
        switch (documentType)
        {
            case "vehicle-photo":
                vendor.VehiclePhoto =
                    blobPath;
                break;

            case "public-liability":
                vendor.PublicLiabilityInsurance =
                    blobPath;
                break;

            case "product-liability":
                vendor.ProductLiabilityInsurance =
                    blobPath;
                break;

            case "food-hygiene":
                vendor.FoodHygieneCertificates =
                    blobPath;
                break;

            case "gas-safety":
                vendor.GasSafetyCertificate =
                    blobPath;
                break;

            case "food-standards":
                vendor.FoodStandardsCertificate =
                    blobPath;
                break;

            case "gas-installation":
                vendor.GasInstallationCertificate =
                    blobPath;
                break;

            case "event-menu":
                vendor.EventMenu =
                    blobPath;
                break;

            case "logo":
                vendor.Logo =
                    blobPath;
                break;
        }
    }


    // ============================================================
    // CLEAN PATH PART
    // ============================================================

    private static string CleanPathPart(
        string value)
    {
        var invalidCharacters =
            Path.GetInvalidFileNameChars();

        foreach (var character in
            invalidCharacters)
        {
            value =
                value.Replace(
                    character.ToString(),
                    string.Empty
                );
        }

        return value;
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