using System.Net;
using Azure.Storage.Blobs;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Api.Functions;

public class TraderDocumentViewFunction
{
    private readonly ITraderRepository _traders;
    private readonly IConfiguration _configuration;

    private const string ContainerName =
        "trader-documents";

    public TraderDocumentViewFunction(
        ITraderRepository traders,
        IConfiguration configuration)
    {
        _traders =
            traders;

        _configuration =
            configuration;
    }


    [Function("TraderDocumentView")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "traderdocuments/view/{eventId}/{passNumber}/{documentType}")]
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


            var trader =
                await _traders.GetTraderAsync(
                    eventId,
                    passNumber
                );


            if (trader == null)
            {
                return await Error(
                    req,
                    HttpStatusCode.NotFound,
                    "Trader was not found."
                );
            }


            var blobPath =
                documentType switch
                {
                    "RiskAssessment" =>
                        trader.RiskAssessment,

                    "PublicLiabilityInsurance" =>
                        trader.PublicLiabilityInsurance,

                    "FoodHandlingCertificate" =>
                        trader.FoodHandlingCertificate,

                    _ =>
                        null
                };


            if (string.IsNullOrWhiteSpace(
                blobPath))
            {
                return await Error(
                    req,
                    HttpStatusCode.NotFound,
                    "The requested document has not been uploaded."
                );
            }


            var connectionString =
                _configuration[
                    "StorageConnection"
                ];


            if (string.IsNullOrWhiteSpace(
                connectionString))
            {
                return await Error(
                    req,
                    HttpStatusCode.InternalServerError,
                    "Storage connection is not configured."
                );
            }


            var blobService =
                new BlobServiceClient(
                    connectionString
                );


            var container =
                blobService.GetBlobContainerClient(
                    ContainerName
                );


            var blob =
                container.GetBlobClient(
                    blobPath
                );


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
                req.CreateResponse(
                    HttpStatusCode.OK
                );


            response.Headers.Add(
                "Content-Type",
                GetContentType(
                    blobPath
                )
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
                "TraderDocumentView error: " +
                ex
            );

            return await Error(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to open the trader document."
            );
        }
    }


    private static string GetContentType(
        string path)
    {
        var extension =
            Path.GetExtension(
                path
            ).ToLowerInvariant();


        return extension switch
        {
            ".pdf" =>
                "application/pdf",

            ".jpg" =>
                "image/jpeg",

            ".jpeg" =>
                "image/jpeg",

            ".png" =>
                "image/png",

            _ =>
                "application/octet-stream"
        };
    }


    private static async Task<HttpResponseData> Error(
        HttpRequestData req,
        HttpStatusCode statusCode,
        string message)
    {
        var response =
            req.CreateResponse(
                statusCode
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