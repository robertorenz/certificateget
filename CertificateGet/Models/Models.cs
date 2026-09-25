using System.Text.Json.Serialization;

namespace CertificateGet.Models;

public enum AcmeEnvironment { Staging, Production }

public enum ChallengeMethod
{
    HttpSelfHosted,
    HttpWebRoot,
    DnsManual,
    DnsCloudflare,
    DnsHostinger
}

public enum CertKeyType { Rsa2048, Rsa3072, Rsa4096, EcdsaP256, EcdsaP384 }

public enum LogLevel { Info, Success, Warning, Error }

/// <summary>A certificate definition (domains + how to validate them) and every issuance made from it.</summary>
public class CertificateProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string Name { get; set; } = "";
    /// <summary>Folder under the store's certificates directory, e.g. "wildcard.example.com_1a2b3c4d5e6f".</summary>
    public string StorageFolder { get; set; } = "";
    public List<string> Domains { get; set; } = new();
    public AcmeEnvironment Environment { get; set; } = AcmeEnvironment.Staging;
    public ChallengeMethod Challenge { get; set; } = ChallengeMethod.HttpSelfHosted;
    public string? WebRootPath { get; set; }
    public int HttpPort { get; set; } = 80;
    public CertKeyType KeyType { get; set; } = CertKeyType.Rsa2048;
    public string? Email { get; set; }
    /// <summary>PFX password, DPAPI-protected for the current Windows user.</summary>
    public string? ProtectedPfxPassword { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public List<IssuedCertificate> History { get; set; } = new();

    [JsonIgnore] public IssuedCertificate? Latest => History.OrderByDescending(h => h.IssuedUtc).FirstOrDefault();
    [JsonIgnore] public bool IsWildcard => Domains.Any(d => d.StartsWith("*."));
    [JsonIgnore] public string DomainsDisplay => string.Join(", ", Domains);
    [JsonIgnore] public string EnvironmentDisplay => Environment == AcmeEnvironment.Production ? "Production" : "Staging";
    [JsonIgnore] public string ChallengeDisplay => Challenge switch
    {
        ChallengeMethod.HttpSelfHosted => "HTTP (built-in server)",
        ChallengeMethod.HttpWebRoot => "HTTP (web root)",
        ChallengeMethod.DnsManual => "DNS (manual)",
        ChallengeMethod.DnsCloudflare => "DNS (Cloudflare)",
        ChallengeMethod.DnsHostinger => "DNS (Hostinger)",
        _ => Challenge.ToString()
    };
    [JsonIgnore] public int? DaysLeft => Latest == null ? null : (int)Math.Floor((Latest.NotAfter - DateTime.UtcNow).TotalDays);
    [JsonIgnore] public string ExpiresDisplay => Latest == null ? "Never issued" : Latest.NotAfter.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    [JsonIgnore] public string StatusText => DaysLeft switch
    {
        null => "Pending",
        < 0 => "Expired",
        1 => "1 day left",
        _ => $"{DaysLeft} days left"
    };
    /// <summary>ok | warn | bad | none — drives the badge colour.</summary>
    [JsonIgnore] public string StatusLevel => DaysLeft switch
    {
        null => "none",
        < 0 => "bad",
        < 15 => "bad",
        < 30 => "warn",
        _ => "ok"
    };
}

/// <summary>One certificate actually issued by Let's Encrypt, stored in its own folder.</summary>
public class IssuedCertificate
{
    public string FolderName { get; set; } = "";
    public DateTime IssuedUtc { get; set; }
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public string Thumbprint { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Issuer { get; set; } = "";
    public List<string> Files { get; set; } = new();

    [JsonIgnore] public string IssuedDisplay => IssuedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    [JsonIgnore] public string ValidDisplay => $"{NotBefore.ToLocalTime():yyyy-MM-dd} → {NotAfter.ToLocalTime():yyyy-MM-dd}";
}

public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; }
    public string Category { get; set; } = "";
    public string? Certificate { get; set; }
    public string Message { get; set; } = "";

    [JsonIgnore] public string TimeDisplay => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
}

public class AppSettings
{
    public string StorePath { get; set; } = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "CertificateGet", "Store");
    public string? DefaultEmail { get; set; }
    public AcmeEnvironment DefaultEnvironment { get; set; } = AcmeEnvironment.Staging;
    public CertKeyType DefaultKeyType { get; set; } = CertKeyType.Rsa2048;
    /// <summary>Cloudflare API token, DPAPI-protected.</summary>
    public string? ProtectedCloudflareToken { get; set; }
    /// <summary>Hostinger API token, DPAPI-protected.</summary>
    public string? ProtectedHostingerToken { get; set; }
    /// <summary>true = "BEGIN PRIVATE KEY" (PKCS#8); false = traditional "BEGIN RSA/EC PRIVATE KEY".</summary>
    public bool KeyFormatPkcs8 { get; set; } = true;
    /// <summary>true = 3DES/SHA1 PFX for old Windows Server / appliances; false = AES-256.</summary>
    public bool PfxLegacyEncryption { get; set; } = true;
    public string DnsResolvers { get; set; } = "1.1.1.1, 8.8.8.8";
    public int DnsPropagationTimeoutSeconds { get; set; } = 600;
    public int RenewWarningDays { get; set; } = 30;
    /// <summary>Format ids written for every new issuance; null = the built-in defaults.</summary>
    public List<string>? IssueFormats { get; set; }
}

/// <summary>A TXT record Let's Encrypt wants to see for a DNS-01 challenge.</summary>
public class DnsTxtRecord
{
    public string Domain { get; set; } = "";
    public string RecordName { get; set; } = "";
    public string Value { get; set; } = "";
    public bool Found { get; set; }
    [JsonIgnore] public string FoundDisplay => Found ? "Visible" : "Not yet visible";
}
