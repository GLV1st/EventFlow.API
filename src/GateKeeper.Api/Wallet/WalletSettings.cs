namespace GateKeeper.Api.Wallet;

public sealed class WalletSettings
{
    public string ApplePassTypeIdentifier { get; init; } = "";
    public string AppleTeamIdentifier { get; init; } = "";
    public string AppleCertificateBase64 { get; init; } = "";
    public string AppleCertificatePassword { get; init; } = "";
    public string AppleWwdrCertificateBase64 { get; init; } = "";
    public string AppleLogoUrl { get; init; } = "";
    public string AppleIconUrl { get; init; } = "";
    public string AppleBackgroundColor { get; init; } = "rgb(107,63,160)";
    public string AppleForegroundColor { get; init; } = "rgb(255,255,255)";
    public string AppleLabelColor { get; init; } = "rgb(255,255,255)";
    public string AppleOrganizationName { get; init; } = "GateKeeper";
    public string AppleWebServiceUrl { get; init; } = "";

    public string GoogleIssuerId { get; init; } = "";
    public string GoogleServiceAccountJson { get; init; } = "";
    public string GoogleWalletOrigin { get; init; } = "https://copcarnival.org.uk";
    public string GoogleLogoUrl { get; init; } = "";

    public bool AppleConfigured =>
        !string.IsNullOrWhiteSpace(ApplePassTypeIdentifier) &&
        !string.IsNullOrWhiteSpace(AppleTeamIdentifier) &&
        !string.IsNullOrWhiteSpace(AppleCertificateBase64) &&
        !string.IsNullOrWhiteSpace(AppleWwdrCertificateBase64);

    public bool GoogleConfigured =>
        !string.IsNullOrWhiteSpace(GoogleIssuerId) &&
        !string.IsNullOrWhiteSpace(GoogleServiceAccountJson);

    public static WalletSettings FromEnvironment()
    {
        return new WalletSettings
        {
            ApplePassTypeIdentifier = Env("APPLE_PASS_TYPE_IDENTIFIER"),
            AppleTeamIdentifier = Env("APPLE_TEAM_IDENTIFIER"),
            AppleCertificateBase64 = Env("APPLE_CERTIFICATE_BASE64"),
            AppleCertificatePassword = Env("APPLE_CERTIFICATE_PASSWORD"),
            AppleWwdrCertificateBase64 = Env("APPLE_WWDR_CERTIFICATE_BASE64"),
            AppleLogoUrl = Env("APPLE_LOGO_URL"),
            AppleIconUrl = Env("APPLE_ICON_URL"),
            AppleBackgroundColor = Env("APPLE_BACKGROUND_COLOR", "rgb(107,63,160)"),
            AppleForegroundColor = Env("APPLE_FOREGROUND_COLOR", "rgb(255,255,255)"),
            AppleLabelColor = Env("APPLE_LABEL_COLOR", "rgb(255,255,255)"),
            AppleOrganizationName = Env("APPLE_ORGANIZATION_NAME", "GateKeeper"),
            AppleWebServiceUrl = Env("APPLE_WEB_SERVICE_URL"),
            GoogleIssuerId = Env("GOOGLE_WALLET_ISSUER_ID"),
            GoogleServiceAccountJson = Env("GOOGLE_WALLET_SERVICE_ACCOUNT_JSON"),
            GoogleWalletOrigin = Env("GOOGLE_WALLET_ORIGIN", "https://copcarnival.org.uk"),
            GoogleLogoUrl = Env("GOOGLE_WALLET_LOGO_URL")
        };
    }

    private static string Env(string name, string fallback = "") =>
        Environment.GetEnvironmentVariable(name) ?? fallback;
}
