using System.Net;
using Azure.Storage.Blobs;
using EventFlow.Storage.Entities;
using EventFlow.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace EventFlow.Api.Functions;

public class TraderDocumentUploadFunction
{
    private readonly ITraderRepository _traders;
    private readonly IConfiguration _configuration;

    private const long MaxFileSize =
        20 * 1024 * 1024;

    private const string ContainerName =
        "trader-documents";

    private static readonly Dictionary<string, string[]>
        AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["risk-assessment"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    },

                ["public-liability"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    },

                ["food-handling"] =
                    new[]
                    {
                        ".pdf",
                        ".jpg",
                        ".jpeg",
                        ".png"
                    }
            };

    public TraderDocumentUploadFunction(
        ITraderRepository traders,
        IConfiguration configuration)
    {
        _traders =
            traders;

        _configuration =
            configuration;
    }

    [Function("UploadTraderDocument")]
    public async Task<HttpResponseData> Upload(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "traderdocuments/upload")]
        HttpRequestData req)
    {
        try
        {
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


            if (!AllowedExtensions.ContainsKey(
                documentType))
            {
                return await Error(
                    req,
                    HttpStatusCode.BadRequest,
                    "Invalid document type."
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
                    "Trader not found."
                );
            }


            if (!string.Equals(
                trader.DocumentUploadToken,
                token,
                StringComparison.Ordinal))
            {
                return await Error(
                    req,
                    HttpStatusCode.Unauthorized,
                    "Invalid upload link."
                );
            }


            if (trader.DocumentUploadTokenExpiresAt.HasValue &&
                trader.DocumentUploadTokenExpiresAt.Value <
                DateTimeOffset.UtcNow)
            {
                return await Error(
                    req,
                    HttpStatusCode.Unauthorized,
                    "This upload link has expired."
                );
            }


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


            fileName =
                Path.GetFileName(
                    fileName
                );

            var extension =
                Path.GetExtension(
                    fileName
                );


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


            using var memoryStream =
                new MemoryStream();

            await req.Body.CopyToAsync(
                memoryStream
            );


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


            var connectionString =
                _configuration[
                    "StorageConnection"
                ];


            if (string.IsNullOrWhiteSpace(
                connectionString))
            {
                throw new InvalidOperationException(
                    "StorageConnection configuration is not configured."
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


            await container.CreateIfNotExistsAsync();


            var safeEventId =
                CleanPathPart(
                    eventId
                );

            var safePassNumber =
                CleanPathPart(
                    passNumber
                );

            var safeDocumentType =
                CleanPathPart(
                    documentType
                );


            var blobName =
                $"{safeEventId}/" +
                $"{safePassNumber}/" +
                $"{safeDocumentType}" +
                extension.ToLowerInvariant();


            var blob =
                container.GetBlobClient(
                    blobName
                );


            await blob.UploadAsync(
                memoryStream,
                overwrite: true
            );


            SetDocumentPath(
                trader,
                documentType,
                blobName
            );


            await _traders.UpdateTraderAsync(
                trader
            );


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

                    blobPath =
                        blobName
                }
            );


            return response;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "UploadTraderDocument error: " +
                ex
            );

            return await Error(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to upload document."
            );
        }
    }


    private static void SetDocumentPath(
        TraderEntity trader,
        string documentType,
        string blobPath)
    {
        switch (documentType)
        {
            case "risk-assessment":

                trader.RiskAssessment =
                    blobPath;

                break;


            case "public-liability":

                trader.PublicLiabilityInsurance =
                    blobPath;

                break;


            case "food-handling":

                trader.FoodHandlingCertificate =
                    blobPath;

                break;
        }
    }


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