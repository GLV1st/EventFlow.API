using System.Net;
using GateKeeper.Api.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace GateKeeper.Api.Functions;

/// <summary>
/// Saves document requirements. Function-level authorization is intentional:
/// do not call this endpoint directly from public browser JavaScript.
/// </summary>
public class SaveTraderDocumentRequirementsFunction(TraderDocumentRequirements requirements)
{
    [Function("SaveTraderDocumentRequirements")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "traderdocuments/requirements")]
        HttpRequestData req)
    {
        var body = await req.ReadFromJsonAsync<TraderDocumentRequirementsUpdateRequest>();
        if (body is null ||
            !TraderDocumentRequirements.IsKnownCategory(body.PitchType) ||
            body.RiskAssessment is null ||
            body.PublicLiabilityInsurance is null ||
            body.FoodHandlingCertificate is null)
        {
            var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
            await badRequest.WriteAsJsonAsync(new
            {
                success = false,
                error = "Provide a valid pitchType and all three document requirement flags.",
                categories = TraderDocumentRequirements.Categories
            });
            return badRequest;
        }

        var saved = await requirements.SaveAsync(
            body.PitchType!,
            body.RiskAssessment.Value,
            body.PublicLiabilityInsurance.Value,
            body.FoodHandlingCertificate.Value);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new
        {
            success = true,
            pitchType = body.PitchType!.Trim(),
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
