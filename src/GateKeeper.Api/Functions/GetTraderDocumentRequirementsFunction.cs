using System.Net;
using GateKeeper.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GateKeeper.Api.Functions;

/// <summary>
/// Exposes the central document rules so connected WordPress pages and
/// reminder workflows can use the same requirements as the API.
/// </summary>
public class GetTraderDocumentRequirementsFunction
{
    [Function("GetTraderDocumentRequirements")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "traderdocuments/requirements")]
        HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var decodedPitchType = query["pitchType"]?.Trim() ?? string.Empty;

        if (!TraderDocumentRequirements.IsKnownCategory(decodedPitchType))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                success = false,
                error = "Unknown stall category.",
                categories = TraderDocumentRequirements.Categories
            });
            return badRequest;
        }

        var requirements = TraderDocumentRequirements.Get(decodedPitchType);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            pitchType = decodedPitchType,
            requiredDocuments = new
            {
                riskAssessment = requirements.RiskAssessment,
                publicLiabilityInsurance = requirements.PublicLiabilityInsurance,
                foodHandlingCertificate = requirements.FoodHandlingCertificate
            },
            requiredDocumentKeys = requirements.RequiredDocumentKeys
        });
        return response;
    }
}
