using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Api.Services;

/// <summary>
/// Single source of truth for mandatory trader documents.
/// Keep category names aligned with the WordPress stall-type list.
/// </summary>
public sealed class TraderDocumentRequirements
{
    private const string Partition = "COP-CARN";
    private const string TableName = "TraderDocumentRules";
    private readonly TableClient _table;

    public TraderDocumentRequirements(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnection"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("StorageConnection configuration is missing.");
        _table = new TableClient(connectionString, TableName);
    }

    // Category names come from the existing WordPress stall-type list.
    // Do not keep a second hard-coded allow-list here: WordPress calls GET
    // for each category, which creates its Azure row with defaults if missing.
    // Retained for error-response compatibility with older API clients only.
    // This list is not used to validate categories; WordPress remains the master list.
    public static IReadOnlyList<string> Categories => new[]
    {
        "Fudge / Sweets", "Clothing / Fashion", "Jewellery / Accessories", "Arts & Crafts",
        "Toys / Children", "Home / Gifts", "Business / Services", "Charity",
        "Entertainment / Activities", "Food / Drink", "Other"
    };

    private static readonly HashSet<string> FoodHygieneRequiredPitchTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Fudge / Sweets",
        "Food / Drink"
    };

    public static bool IsKnownCategory(string? pitchType) =>
        !string.IsNullOrWhiteSpace(pitchType) && pitchType.Trim().Length <= 120;

    public async Task<TraderDocumentRequirement> GetAsync(string pitchType)
    {
        var normalizedPitchType = NormalizeCategory(pitchType);
        await EnsureTableAsync();
        var rowKey = ToRowKey(normalizedPitchType);
        var existing = await _table.GetEntityIfExistsAsync<TableEntity>(Partition, rowKey);
        if (existing.HasValue) return FromEntity(existing.Value);

        var defaults = DefaultFor(normalizedPitchType);
        try { await _table.AddEntityAsync(ToEntity(normalizedPitchType, defaults)); }
        catch (RequestFailedException ex) when (ex.Status == 409) { }
        var saved = await _table.GetEntityAsync<TableEntity>(Partition, rowKey);
        return FromEntity(saved.Value);
    }

    public async Task<TraderDocumentRequirement> SaveAsync(
        string pitchType, bool riskAssessment, bool publicLiabilityInsurance, bool foodHandlingCertificate)
    {
        var normalized = NormalizeCategory(pitchType);
        await EnsureTableAsync();
        var rules = new TraderDocumentRequirement(riskAssessment, publicLiabilityInsurance, foodHandlingCertificate);
        await _table.UpsertEntityAsync(ToEntity(normalized, rules), TableUpdateMode.Replace);
        return rules;
    }

    private async Task EnsureTableAsync() => await _table.CreateIfNotExistsAsync();

    private static string NormalizeCategory(string pitchType)
    {
        var normalized = pitchType.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 120)
            throw new ArgumentException("A valid stall category is required.", nameof(pitchType));
        return normalized;
    }

    private static TraderDocumentRequirement DefaultFor(string pitchType) => new(
        RiskAssessment: true,
        PublicLiabilityInsurance: true,
        FoodHandlingCertificate: FoodHygieneRequiredPitchTypes.Contains(pitchType));

    private static string ToRowKey(string pitchType) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pitchType))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static TableEntity ToEntity(string pitchType, TraderDocumentRequirement rules) => new(Partition, ToRowKey(pitchType))
    {
        ["PitchType"] = pitchType,
        ["RiskAssessment"] = rules.RiskAssessment,
        ["PublicLiabilityInsurance"] = rules.PublicLiabilityInsurance,
        ["FoodHandlingCertificate"] = rules.FoodHandlingCertificate,
        ["UpdatedUtc"] = DateTimeOffset.UtcNow
    };

    private static TraderDocumentRequirement FromEntity(TableEntity entity) => new(
        entity.GetBoolean("RiskAssessment") ?? true,
        entity.GetBoolean("PublicLiabilityInsurance") ?? true,
        entity.GetBoolean("FoodHandlingCertificate") ?? false);
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

public sealed class TraderDocumentRequirementsUpdateRequest
{
    public string? PitchType { get; set; }
    public bool? RiskAssessment { get; set; }
    public bool? PublicLiabilityInsurance { get; set; }
    public bool? FoodHandlingCertificate { get; set; }
}
