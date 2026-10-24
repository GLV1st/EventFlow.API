namespace GateKeeper.Api.Services;

/// <summary>
/// Single source of truth for mandatory trader documents.
/// Keep category names aligned with the WordPress stall-type list.
/// </summary>
public static class TraderDocumentRequirements
{
    private static readonly HashSet<string> ValidPitchTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fudge / Sweets",
        "Clothing / Fashion",
        "Jewellery / Accessories",
        "Arts & Crafts",
        "Toys / Children",
        "Home / Gifts",
        "Business / Services",
        "Charity",
        "Entertainment / Activities",
        "Food / Drink",
        "Other"
    };

    private static readonly HashSet<string> FoodHygieneRequiredPitchTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fudge / Sweets",
        "Food / Drink"
    };

    public static IReadOnlyList<string> Categories => ValidPitchTypes
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public static TraderDocumentRequirement Get(string? pitchType)
    {
        var normalizedPitchType = pitchType?.Trim() ?? string.Empty;

        // Risk Assessment and Public Liability are mandatory for all current
        // stall categories. Food Hygiene is mandatory only for food-related
        // categories explicitly agreed for this event.
        return new TraderDocumentRequirement(
            RiskAssessment: true,
            PublicLiabilityInsurance: true,
            FoodHandlingCertificate: FoodHygieneRequiredPitchTypes.Contains(normalizedPitchType));
    }

    public static bool IsKnownCategory(string? pitchType) =>
        !string.IsNullOrWhiteSpace(pitchType) && ValidPitchTypes.Contains(pitchType.Trim());
}

public sealed record TraderDocumentRequirement(
    bool RiskAssessment,
    bool PublicLiabilityInsurance,
    bool FoodHandlingCertificate)
{
    public IReadOnlyList<string> RequiredDocumentKeys
    {
        get
        {
            var required = new List<string>();
            if (RiskAssessment) required.Add("riskAssessment");
            if (PublicLiabilityInsurance) required.Add("publicLiabilityInsurance");
            if (FoodHandlingCertificate) required.Add("foodHandlingCertificate");
            return required;
        }
    }
}
