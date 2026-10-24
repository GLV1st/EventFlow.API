using System.Net;
using GateKeeper.Api.Services;
using GateKeeper.Storage.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GateKeeper.Api.Functions;

public class GetTraderDocumentStatusFunction
{
    private readonly ITraderRepository _traders;

    public GetTraderDocumentStatusFunction(ITraderRepository traders)
    {
        _traders = traders;
    }

    [Function("GetTraderDocumentStatus")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "traderdocuments/status/{eventId}/{passNumber}")]
        HttpRequestData req,
        string eventId,
        string passNumber)
    {
        try
        {
            var query =
                System.Web.HttpUtility.ParseQueryString(req.Url.Query);

            var token = query["token"];

            if (string.IsNullOrWhiteSpace(eventId))
                return await Error(req, HttpStatusCode.BadRequest, "EventId is required.");

            if (string.IsNullOrWhiteSpace(passNumber))
                return await Error(req, HttpStatusCode.BadRequest, "PassNumber is required.");

            if (string.IsNullOrWhiteSpace(token))
                return await Error(req, HttpStatusCode.BadRequest, "Upload token is required.");

            var trader =
                await _traders.GetTraderAsync(
                    eventId,
                    passNumber
                );

            if (trader == null)
                return await Error(
                    req,
                    HttpStatusCode.NotFound,
                    "Trader not found."
                );

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

            var requirements = TraderDocumentRequirements.Get(trader.PitchType);

            var documents = new
            {
                riskAssessment = !string.IsNullOrWhiteSpace(trader.RiskAssessment),
                publicLiabilityInsurance = !string.IsNullOrWhiteSpace(trader.PublicLiabilityInsurance),
                foodHandlingCertificate = !string.IsNullOrWhiteSpace(trader.FoodHandlingCertificate)
            };

            var missingRequiredDocuments = new List<string>();
            if (requirements.RiskAssessment && !documents.riskAssessment)
                missingRequiredDocuments.Add("riskAssessment");
            if (requirements.PublicLiabilityInsurance && !documents.publicLiabilityInsurance)
                missingRequiredDocuments.Add("publicLiabilityInsurance");
            if (requirements.FoodHandlingCertificate && !documents.foodHandlingCertificate)
                missingRequiredDocuments.Add("foodHandlingCertificate");

            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new
            {
                success = true,
                eventId = trader.EventId,
                traderPassNumber = trader.TraderPassNumber,
                companyName = trader.CompanyName,
                pitchType = trader.PitchType,
                requiredDocuments = new
                {
                    riskAssessment = requirements.RiskAssessment,
                    publicLiabilityInsurance = requirements.PublicLiabilityInsurance,
                    foodHandlingCertificate = requirements.FoodHandlingCertificate
                },
                documents,
                missingRequiredDocuments,
                allRequiredDocumentsUploaded = missingRequiredDocuments.Count == 0
            });

            return response;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                "GetTraderDocumentStatus error: " +
                ex
            );

            return await Error(
                req,
                HttpStatusCode.InternalServerError,
                "Unable to retrieve trader document status."
            );
        }
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