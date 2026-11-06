using System.Net;
using EventFlow.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EventFlow.Api.Functions;

/// <summary>Reads rules from Azure. Any category supplied by the WordPress master list
/// is created in Azure with defaults on first read.</summary>
public class GetTraderDocumentRequirementsFunction(TraderDocumentRequirements requirements)
{
    [Function("GetTraderDocumentRequirements")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "traderdocuments/requirements")]
        HttpRequestData req)
    {
        var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var pitchType = query["pitchType"]?.Trim() ?? string.Empty;
        if (!TraderDocumentRequirements.IsKnownCategory(pitchType))
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new { success = false, error = "A valid stall category is required." });
            return badRequest;
        }

        var saved = await requirements.GetAsync(pitchType);
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            pitchType,
            requiredDocuments = new
            {
                riskAssessment = saved.RiskAssessment,
                publicLiabilityInsurance = saved.PublicLiabilityInsurance,
                foodHandlingCertificate = saved.FoodHandlingCertificate
            },
            requiredDocumentKeys = saved.RequiredDocumentKeys
        });
        return response;
    }
}
