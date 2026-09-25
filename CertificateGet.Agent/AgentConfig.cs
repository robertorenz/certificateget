using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;

namespace CertificateGet.Agent;

/// <summary>agent.json — lives next to the executable and is re-read on every request, so edits apply immediately.</summary>
public class AgentConfig
{
    public int Port { get; set; } = 9443;
    /// <summary>SHA-256 (hex) of the API key. The key itself is only shown once, by "install" or "newkey".</summary>
    public string ApiKeyHash { get; set; } = "";
    /// <summary>Optional list of client IPs allowed to connect. Empty = any.</summary>
    public List<string> AllowedIps { get; set; } = new();
    public string TlsPfx { get; set; } = "agent-tls.pfx";
    public string TlsPassword { get; set; } = "";
    public int KeepBackups { get; set; } = 5;
    public List<Slot> Slots { get; set; } = new();

    public static string Folder => AppContext.BaseDirectory;
    public static string FilePath => Path.Combine(Folder, "agent.json");
    public static string LogPath => Path.Combine(Folder, "agent.log");

    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public static AgentConfig Load()
    {
        if (!File.Exists(FilePath)) throw new FileNotFoundException($"Configuration not found: {FilePath}. Run \"install\" first.");
        return JsonSerializer.Deserialize<AgentConfig>(File.ReadAllText(FilePath), JsonOptions)
               ?? throw new InvalidDataException("agent.json is empty.");
    }

    public void Save()
    {
        File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(FilePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    public static string Hash(string apiKey) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(apiKey)));

    public bool CheckKey(string? apiKey) =>
        !string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(ApiKeyHash) &&
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(Hash(apiKey)), Convert.FromHexString(ApiKeyHash));

    /// <summary>Creates a new random API key, stores its hash, and returns the key.</summary>
    public string NewApiKey()
    {
        var key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        ApiKeyHash = Hash(key);
        return key;
    }

    public X509Certificate2 LoadOrCreateTlsCertificate()
    {
        var path = Path.Combine(Folder, TlsPfx);
        if (!File.Exists(path) || string.IsNullOrEmpty(TlsPassword))
        {
            TlsPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            using var rsa = RSA.Create(2048);
            var host = Environment.MachineName;
            var req = new CertificateRequest($"CN=CertificateGet Agent {host}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            var san = new SubjectAlternativeNameBuilder();
            san.AddDnsName(host);
            san.AddDnsName("localhost");
            req.CertificateExtensions.Add(san.Build());
            req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
            using var cert = req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20));
            File.WriteAllBytes(path, cert.Export(X509ContentType.Pfx, TlsPassword));
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
            Save();
        }
        return X509CertificateLoader.LoadPkcs12FromFile(path, TlsPassword);
    }

    public static string Fingerprint(X509Certificate2 cert) => Convert.ToHexString(SHA256.HashData(cert.RawData));

    public static AgentConfig Sample() => new()
    {
        Slots =
        {
            new Slot
            {
                Name = "example-com",
                Description = "Example: two NetTalk instances using the same certificate",
                Destinations =
                {
                    new Destination
                    {
                        Name = "Web app 1",
                        Folder = OperatingSystem.IsWindows() ? @"C:\Apps\WebApp1\certificates" : "/etc/haproxy/certs",
                        Files = OperatingSystem.IsWindows()
                            ? new() { new FileSpec { Source = "fullchain", FileName = "example.com.crt" }, new FileSpec { Source = "key", FileName = "example.com.key" } }
                            : new() { new FileSpec { Source = "combined", FileName = "example.com.pem" } },
                        RestartServices = OperatingSystem.IsWindows() ? new() { "WebApp1Service" } : new(),
                        Commands = OperatingSystem.IsWindows() ? new() : new() { "haproxy -c -f /etc/haproxy/haproxy.cfg && systemctl reload haproxy" }
                    }
                }
            }
        }
    };
}

/// <summary>What a certificate from the app maps to on this server: one or more destinations.</summary>
public class Slot
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<Destination> Destinations { get; set; } = new();
    /// <summary>Commands run once after all destinations were written and their services restarted.</summary>
    public List<string> Commands { get; set; } = new();
}

public class Destination
{
    public string Name { get; set; } = "";
    /// <summary>"Files" (default): write the files below into Folder. "TSplus": import a PFX with TSplus's CertificateManager (TSplus 15+).
    /// "TSplusJks": write cert.jks into TSplus\Clients\webserver and restart the TSplus web server (versions that use cert.jks).</summary>
    public string Kind { get; set; } = "Files";
    /// <summary>TSplus only: its cert folder (default C:\Program Files (x86)\TSplus\UserDesktop\files\cert).</summary>
    public string? TsplusCertFolder { get; set; }
    /// <summary>TSplusJks only: TSplus install folder (default C:\Program Files (x86)\TSplus).</summary>
    public string? TsplusFolder { get; set; }
    public string Folder { get; set; } = "";
    public List<FileSpec> Files { get; set; } = new();
    /// <summary>Windows service names (or systemd units on Linux) restarted after the files are written.</summary>
    public List<string> RestartServices { get; set; } = new();
    /// <summary>Windows only: desktop programs (.exe) to close and start again on the same user's desktop.</summary>
    public List<ProgramSpec> RestartPrograms { get; set; } = new();
    /// <summary>Commands run after the files are written (cmd.exe on Windows, /bin/sh on Linux).</summary>
    public List<string> Commands { get; set; } = new();
}

public class FileSpec
{
    /// <summary>combined, combined-keyfirst, fullchain, fullchain-root, cer, crt, der, chain, key, encrypted-key, pfx, p12, p7b, k8s, jks</summary>
    public string Source { get; set; } = "";
    public string FileName { get; set; } = "";
}
