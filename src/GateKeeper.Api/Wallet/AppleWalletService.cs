using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using GateKeeper.Storage.Entities;

namespace GateKeeper.Api.Wallet;

public sealed class AppleWalletService
{
    private readonly WalletSettings _settings;
    private readonly HttpClient _http;

    public AppleWalletService(WalletSettings settings, IHttpClientFactory httpClientFactory)
    {
        _settings = settings;
        _http = httpClientFactory.CreateClient("wallet");
    }

    public async Task<byte[]> CreateBundleAsync(IReadOnlyList<TicketEntity> tickets, string eventName)
    {
        if (tickets.Count == 0) throw new ArgumentException("At least one ticket is required.");
        if (tickets.Count > 10) throw new InvalidOperationException("Apple Wallet bundles support a maximum of 10 passes.");

        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var ticket in tickets)
            {
                var pass = await CreatePassAsync(ticket, eventName);
                var entry = zip.CreateEntry(ticket.RowKey + ".pkpass", CompressionLevel.Optimal);
                using var stream = entry.Open();
                await stream.WriteAsync(pass);
            }
        }
        return output.ToArray();
    }

    public async Task<byte[]> CreatePassAsync(TicketEntity ticket, string eventName)
    {
        EnsureConfigured();

        using var temp = new MemoryStream();
        using (var zip = new ZipArchive(temp, ZipArchiveMode.Create, true))
        {
            AddText(zip, "pass.json", JsonSerializer.Serialize(BuildPass(ticket, eventName)));

            var icon = await DownloadOptionalAsync(_settings.AppleIconUrl);
            if (icon.Length > 0)
                AddBytes(zip, "icon.png", icon);

            var logo = await DownloadOptionalAsync(_settings.AppleLogoUrl);
            if (logo.Length > 0)
                AddBytes(zip, "logo.png", logo);
        }

        // Re-open as a directory-like in-memory package so we can create the
        // Apple manifest and detached PKCS#7 signature before producing .pkpass.
        temp.Position = 0;
        using var sourceZip = new ZipArchive(temp, ZipArchiveMode.Read, true);
        var files = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in sourceZip.Entries)
        {
            using var ms = new MemoryStream();
            using var stream = entry.Open();
            await stream.CopyToAsync(ms);
            files[entry.FullName] = ms.ToArray();
        }

        var manifest = files.ToDictionary(
            x => x.Key,
            x => Convert.ToHexString(SHA256.HashData(x.Value)).ToLowerInvariant(),
            StringComparer.Ordinal);

        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        files["manifest.json"] = manifestBytes;

        var signer = LoadCertificate();
        var wwdr = LoadWwdrCertificate();
        var cms = new SignedCms(new ContentInfo(manifestBytes), detached: true);
        var signerInfo = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, signer)
        {
            IncludeOption = X509IncludeOption.EndCertOnly
        };
        signerInfo.Certificates.Add(wwdr);
        cms.ComputeSignature(signerInfo);
        files["signature"] = cms.Encode();

        using var output = new MemoryStream();
        using (var finalZip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            foreach (var file in files)
                AddBytes(finalZip, file.Key, file.Value);
        }

        return output.ToArray();
    }

    private object BuildPass(TicketEntity ticket, string eventName)
    {
        var serial = ticket.RowKey;
        var description = $"{eventName} ticket {serial}";
        var barcode = new
        {
            format = "PKBarcodeFormatCode128",
            message = serial,
            messageEncoding = "iso-8859-1",
            altText = serial
        };

        var pass = new Dictionary<string, object?>
        {
            ["formatVersion"] = 1,
            ["passTypeIdentifier"] = _settings.ApplePassTypeIdentifier,
            ["serialNumber"] = serial,
            ["teamIdentifier"] = _settings.AppleTeamIdentifier,
            ["organizationName"] = _settings.AppleOrganizationName,
            ["description"] = description,
            ["logoText"] = eventName,
            ["foregroundColor"] = _settings.AppleForegroundColor,
            ["backgroundColor"] = _settings.AppleBackgroundColor,
            ["labelColor"] = _settings.AppleLabelColor,
            ["barcode"] = barcode,
            ["barcodes"] = new[] { barcode },
            ["eventTicket"] = new
            {
                primaryFields = new[]
                {
                    new { key = "event", label = "EVENT", value = eventName }
                },
                secondaryFields = new[]
                {
                    new { key = "type", label = "TICKET TYPE", value = ticket.TicketType },
                    new { key = "number", label = "TICKET", value = serial }
                },
                auxiliaryFields = new[]
                {
                    new { key = "holder", label = "TICKET HOLDER", value = ticket.CustomerName }
                }
            }
        };

        if (DateTime.TryParse(ticket.EventDate, out var date))
        {
            pass["relevantDate"] = date.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
        }

        if (!string.IsNullOrWhiteSpace(_settings.AppleWebServiceUrl))
        {
            pass["webServiceURL"] = _settings.AppleWebServiceUrl;
            pass["authenticationToken"] = CreateAuthToken(serial);
        }

        return pass;
    }

    private string CreateAuthToken(string serial)
    {
        var secret = Environment.GetEnvironmentVariable("APPLE_PASS_AUTH_SECRET") ?? "";
        if (string.IsNullOrWhiteSpace(secret))
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(serial))).ToLowerInvariant();

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(serial))).ToLowerInvariant();
    }

    private X509Certificate2 LoadCertificate()
    {
        var bytes = Convert.FromBase64String(_settings.AppleCertificateBase64);
        return new X509Certificate2(bytes, _settings.AppleCertificatePassword,
            X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
    }

    private X509Certificate2 LoadWwdrCertificate()
    {
        var bytes = Convert.FromBase64String(_settings.AppleWwdrCertificateBase64);
        return new X509Certificate2(bytes);
    }

    private void EnsureConfigured()
    {
        if (!_settings.AppleConfigured)
            throw new InvalidOperationException("Apple Wallet is not configured. Set the APPLE_* application settings first.");
    }

    private async Task<byte[]> DownloadOptionalAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return Array.Empty<byte>();
        using var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return Array.Empty<byte>();
        return await response.Content.ReadAsByteArrayAsync();
    }

    private static void AddText(ZipArchive zip, string name, string value) =>
        AddBytes(zip, name, Encoding.UTF8.GetBytes(value));

    private static void AddBytes(ZipArchive zip, string name, byte[] bytes)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }
}
