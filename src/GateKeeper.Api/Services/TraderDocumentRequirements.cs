using Azure;
using Azure.Data.Tables;
using Microsoft.Extensions.Configuration;

namespace GateKeeper.Api.Services;

/// <summary>
/// Azure Table Storage-backed source of truth for trader document requirements.
/// Defaults are created only when a category has no saved row, so later edits persist.
/// </summary>
public sealed class TraderDocumentRequirements
{
    private const string Partition = "COP-CARN";
    private const string TableName = "TraderDocumentRules";
    private readonly TableClient _table;

    private static readonly string[] CategoryNames =
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

    public static IReadOnlyList<string> Categories => CategoryNames;

    public TraderDocumentRequirements(IConfiguration configuration)
    {
        var connectionString = configuration["StorageConnection"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("StorageConnection configuration is missing.");
        }

        _table = new TableClient(connectionString, TableName);
    }

    public static bool IsKnownCategory(string? pitchType) =>
        !string.IsNullOrWhiteSpace(pitchType) &&
        CategoryNames.Contains(pitchType.Trim(), StringComparer.OrdinalIgnoreCase);

    public async Task<TraderDocumentRequirement> GetAsync(string pitchType)
    {
        var normalized = NormalizeCategory(pitchType);
        await EnsureTableAsync();
        var rowKey = ToRowKey(normalized);

        var existing = await _table.GetEntityIfExistsAsync<TableEntity>(Partition, rowKey);
        if (existing.HasValue)
        {
            return FromEntity(existing.Value);
        }

        var defaults = DefaultFor(normalized);
        var entity = ToEntity(normalized, defaults);
        try
        {
            await _table.AddEntityAsync(entity);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            // Another request may have inserted this default at the same time.
        }

        var saved = await _table.GetEntityAsync<TableEntity>(Partition, rowKey);
        return FromEntity(saved.Value);
    }

    public async Task<TraderDocumentRequirement> SaveAsync(
        string pitchType,
        bool riskAssessment,
        bool publicLiabilityInsurance,
        bool foodHandlingCertificate)
    {
        var normalized = NormalizeCategory(pitchType);
        await EnsureTableAsync();

        var requirements = new TraderDocumentRequirement(
            riskAssessment,
            publicLiabilityInsurance,
            foodHandlingCertificate);

        await _table.UpsertEntityAsync(ToEntity(normalized, requirements), TableUpdateMode.Replace);
        return requirements;
    }

    private async Task EnsureTableAsync()
    {
        await _table.CreateIfNotExistsAsync();
    }

    private static string NormalizeCategory(string pitchType)
    {
        var normalized = pitchType.Trim();
        var match = CategoryNames.FirstOrDefault(
            x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            throw new ArgumentException("Unknown stall category.", nameof(pitchType));
        }

        return match;
    }

    private static TraderDocumentRequirement DefaultFor(string pitchType) => new(
        RiskAssessment: true,
        PublicLiabilityInsurance: true,
        FoodHandlingCertificate:
            string.Equals(pitchType, "Fudge / Sweets", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pitchType, "Food / Drink", StringComparison.OrdinalIgnoreCase));

    private static string ToRowKey(string pitchType) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(pitchType))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static TableEntity ToEntity(string pitchType, TraderDocumentRequirement requirements)
    {
        var entity = new TableEntity(Partition, ToRowKey(pitchType))
        {
            ["PitchType"] = pitchType,
            ["RiskAssessment"] = requirements.RiskAssessment,
            ["PublicLiabilityInsurance"] = requirements.PublicLiabilityInsurance,
            ["FoodHandlingCertificate"] = requirements.FoodHandlingCertificate,
            ["UpdatedUtc"] = DateTimeOffset.UtcNow
        };
        return entity;
    }

    private static TraderDocumentRequirement FromEntity(TableEntity entity) => new(
        entity.GetBoolean("RiskAssessment") ?? false,
        entity.GetBoolean("PublicLiabilityInsurance") ?? false,
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
