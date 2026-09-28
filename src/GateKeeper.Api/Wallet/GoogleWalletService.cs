using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GateKeeper.Storage.Entities;

namespace GateKeeper.Api.Wallet;

public sealed class GoogleWalletService
{
    private readonly WalletSettings _settings;

    public GoogleWalletService(WalletSettings settings)
    {
        _settings = settings;
    }

    public string CreateSaveUrl(TicketEntity ticket, string eventName, string eventId)
    {
        EnsureConfigured();
        return CreateSaveUrl(new[] { ticket }, eventName, eventId);
    }

    public string CreateSaveUrl(IReadOnlyList<TicketEntity> tickets, string eventName, string eventId)
    {
        EnsureConfigured();
        if (tickets.Count == 0) throw new ArgumentException("At least one ticket is required.");

        var credentials = JsonSerializer.Deserialize<GoogleServiceAccount>(_settings.GoogleServiceAccountJson)
            ?? throw new InvalidOperationException("Invalid Google Wallet service account JSON.");

        var classId = $"{_settings.GoogleIssuerId}.{SafeId(eventId)}";
        var objects = tickets.Select(t => new Dictionary<string, object?>
        {
            ["id"] = $"{_settings.GoogleIssuerId}.{SafeId(t.RowKey)}",
            ["classId"] = classId,
            ["state"] = "ACTIVE",
            ["ticketHolderName"] = t.CustomerName,
            ["ticketNumber"] = t.RowKey,
            ["ticketType"] = new { defaultValue = new { language = "en-GB", value = t.TicketType } },
            ["barcode"] = new { type = "CODE_128", value = t.RowKey, alternateText = t.RowKey },
            ["hexBackgroundColor"] = "#6B3FA0"
        }).ToArray();

        var eventClass = new Dictionary<string, object?>
        {
            ["id"] = classId,
            ["issuerName"] = "GateKeeper",
            ["reviewStatus"] = "UNDER_REVIEW",
            ["eventId"] = SafeId(eventId),
            ["eventName"] = new { defaultValue = new { language = "en-GB", value = eventName } },
            ["venue"] = new
            {
                name = new { defaultValue = new { language = "en-GB", value = eventName } },
                address = new { defaultValue = new { language = "en-GB", value = "Event venue" } }
            },
            ["dateTime"] = BuildEventDateTime(tickets[0].EventDate),
            ["hexBackgroundColor"] = "#6B3FA0"
        };

        var claims = new Dictionary<string, object?>
        {
            ["iss"] = credentials.client_email,
            ["aud"] = "google",
            ["typ"] = "savetowallet",
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["origins"] = new[] { _settings.GoogleWalletOrigin },
            ["payload"] = new Dictionary<string, object?>
            {
                ["eventTicketClasses"] = new[] { eventClass },
                ["eventTicketObjects"] = objects
            }
        };

        var token = SignJwt(claims, credentials.private_key);
        return "https://pay.google.com/gp/v/save/" + token;
    }

    private static object BuildEventDateTime(string eventDate)
    {
        if (DateTime.TryParse(eventDate, out var date))
        {
            var day = date.ToString("yyyy-MM-dd");
            return new
            {
                start = day + "T00:00:00",
                end = day + "T23:59:59"
            };
        }
        return new { start = eventDate };
    }

    private static string SignJwt(Dictionary<string, object?> claims, string privateKeyPem)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(claims));
        var data = Encoding.UTF8.GetBytes(header + "." + payload);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return header + "." + payload + "." + Base64Url(signature);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string SafeId(string value) =>
        new string(value.Where(c => char.IsLetterOrDigit(c) || c == '_' || c == '.').ToArray());

    private void EnsureConfigured()
    {
        if (!_settings.GoogleConfigured)
            throw new InvalidOperationException("Google Wallet is not configured. Set GOOGLE_WALLET_ISSUER_ID and GOOGLE_WALLET_SERVICE_ACCOUNT_JSON first.");
    }

    private sealed class GoogleServiceAccount
    {
        public string client_email { get; set; } = "";
        public string private_key { get; set; } = "";
    }
}
