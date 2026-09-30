using System.Net;
using Azure.Storage.Blobs;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Api.Functions;

public class CateringDocumentViewFunction
{
    private readonly ICateringVendorRepository _cateringVendors;
    private readonly IConfiguration _configuration;

    private const string ContainerName = "catering-documents";

    public CateringDocumentViewFunction(
        ICateringVendorRepository cateringVendors,
        IConfiguration configuration)
    {
        _cateringVendors = cateringVendors;
        _configuration = configuration;
    }

    [Function("CateringDocumentView")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "cateringdocuments/view/{eventId}/{passNumber}/{documentType}")]
        HttpRequestData req,
        string eventId,
        string passNumber,
        string documentType)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(eventId) ||
                string.IsNullOrWhiteSpace(passNumber) ||
                string.IsNullOrWhiteSpace(documentType))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "EventId, pass number and document type are required."
                );
            }

            var vendor =
                await _cateringVendors.GetCateringVendorAsync(
                    eventId,
                    passNumber
                );

            if (vendor == null)
            {
                return await Error(
                    req,
                    HttpStatusCode.NotFound,
                    "Catering vendor was not found."
                );
            }

            var blobPath = documentType switch
            {
                "VehiclePhoto" => vendor.VehiclePhoto,
                "PublicLiabilityInsurance" => vendor.PublicLiabilityInsurance,
                "ProductLiabilityInsurance" => vendor.ProductLiabilityInsurance,
                "FoodHygieneCertificates" => vendor.FoodHygieneCertificates,
                "GasSafetyCertificate" => vendor.GasSafetyCertificate,
                "FoodStandardsCertificate" => vendor.FoodStandardsCertificate,
                "GasInstallationCertificate" => vendor.GasInstallationCertificate,
                "EventMenu" => vendor.EventMenu,
                "Logo" => vendor.Logo,
                _ => null
            };

            if (string.IsNullOrWhiteSpace(blobPath))
            {
                return await Error(
                    req,
                    HttpStatusCode.NotFound,
                    "The requested document has not been uploaded."
                );
            }

            var connectionString =
                _configuration["StorageConnection"];

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return await Error(
                    req,
                    HttpStatusCode.InternalServerError,
                    "Storage connection is not configured."
                );
            }

            var blobService =
                new BlobServiceClient(connectionString);

            var container =
                blobService.GetBlobContainerClient(ContainerName);

            var blob =
                container.GetBlobClient(blobPath);

            if (!await blob.ExistsAsync())
            {
                return await Error(
                    req,
                    HttpStatusCode.NotFound,
                    "The document file could not be found in storage."
                );
            }

            var download =
                await blob.DownloadContentAsync();

            var response =
                req.CreateResponse(HttpStatusCode.OK);

            response.Headers.Add(
                "Content-Type",
                GetContentType(blobPath)
            );

            response.Headers.Add(
                "Content-Disposition",
                "inline"
            );

            response.Headers.Add(
                "Cache-Control",
                "private, max-age=300"
            );

            await response.Body.WriteAsync(
                download.Value.Content.ToArray()
            );

            return response;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "CateringDocumentView error: " + ex
            );

            return await Error(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to open the catering document."
            );
        }
    }

    private static string GetContentType(string path)
    {
        var extension =
            Path.GetExtension(path).ToLowerInvariant();

        return extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" => "image/jpeg",
            ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    private static async Task<HttpResponseData> Error(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message)
    {
        var response =
            req.CreateResponse(statusCode);

        await response.WriteAsJsonAsync(
            new
            {
                error = message
            }
        );

        return response;
    }
}
