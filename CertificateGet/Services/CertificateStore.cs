using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using CertificateGet.Models;

namespace CertificateGet.Services;

/// <summary>The output formats written for every issuance.</summary>
public static class CertFileKind
{
    public const string Pfx = ".pfx";
    public const string Cer = ".cer";
    public const string DerCer = "-der.cer";
    public const string Key = ".key";
    public const string Chain = "-chain.cer";
    public const string FullChain = "-fullchain.pem";
    public const string Combined = "-combined.pem";

    public static readonly (string Suffix, string Label, string Description)[] All =
    {
        (Pfx, "PFX / PKCS#12", "Certificate + chain + private key in one password-protected file (IIS, Azure, Exchange, Windows)."),
        (Cer, "CER (PEM)", "The certificate only, Base-64 PEM text."),
        (Key, "KEY (PEM)", "The private key, PEM text. Keep it secret."),
        (FullChain, "Full chain PEM", "Certificate followed by the intermediate chain (nginx ssl_certificate, Apache 2.4.8+)."),
        (Combined, "Combined PEM", "Full chain + private key in a single file (HAProxy, Webmin, many appliances)."),
        (Chain, "Chain (PEM)", "Intermediate certificates only (Apache SSLCertificateChainFile)."),
        (DerCer, "CER (DER binary)", "The certificate only, binary DER encoding (Java, some Windows tools)."),
    };
}

public static class CertificateStore
{
    public static event Action? Changed;

    public static void RaiseChanged() => Changed?.Invoke();

    public static string SafeName(string name)
    {
        var s = name.Replace("*", "wildcard");
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    public static string BaseFileName(CertificateProfile p) => SafeName(p.Domains.FirstOrDefault() ?? p.Name);

    public static string ProfileFolder(CertificateProfile p)
    {
        if (string.IsNullOrEmpty(p.StorageFolder)) p.StorageFolder = $"{SafeName(p.Name)}_{p.Id}";
        return Path.Combine(SettingsService.CertificatesPath, p.StorageFolder);
    }

    public static string IssuanceFolder(CertificateProfile p, IssuedCertificate i) => Path.Combine(ProfileFolder(p), i.FolderName);

    public static List<CertificateProfile> LoadAll()
    {
        var list = new List<CertificateProfile>();
        if (!Directory.Exists(SettingsService.CertificatesPath)) return list;
        foreach (var dir in Directory.GetDirectories(SettingsService.CertificatesPath))
        {
            var file = Path.Combine(dir, "profile.json");
            if (!File.Exists(file)) continue;
            try
            {
                var p = JsonSerializer.Deserialize<CertificateProfile>(File.ReadAllText(file), Json.Options);
                if (p == null) continue;
                p.StorageFolder = Path.GetFileName(dir);
                list.Add(p);
            }
            catch (Exception ex)
            {
                ActivityLog.Warning("Store", $"Could not read {file}: {ex.Message}");
            }
        }
        return list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static void Save(CertificateProfile p)
    {
        var folder = ProfileFolder(p);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "profile.json"), JsonSerializer.Serialize(p, Json.Options));
        RaiseChanged();
    }

    public static void Delete(CertificateProfile p)
    {
        var folder = ProfileFolder(p);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        RaiseChanged();
    }

    /// <summary>Writes every format for a freshly issued certificate and records it in the profile history.</summary>
    public static IssuedCertificate SaveIssuance(CertificateProfile p, string leafPem, IList<string> issuerPems, string keyPem, string? pfxPassword)
    {
        var issued = new IssuedCertificate
        {
            FolderName = DateTime.Now.ToString("yyyy-MM-dd_HHmmss"),
            IssuedUtc = DateTime.UtcNow
        };
        var folder = IssuanceFolder(p, issued);
        Directory.CreateDirectory(folder);

        issued.Files = WriteFiles(folder, BaseFileName(p), leafPem, issuerPems, keyPem, pfxPassword, p.Name,
            CertFileKind.All.Select(k => k.Suffix));

        using var cert = X509Certificate2.CreateFromPem(leafPem);
        issued.NotBefore = cert.NotBefore.ToUniversalTime();
        issued.NotAfter = cert.NotAfter.ToUniversalTime();
        issued.Thumbprint = cert.Thumbprint;
        issued.SerialNumber = cert.SerialNumber;
        issued.Issuer = cert.GetNameInfo(X509NameType.SimpleName, true);

        p.History.Add(issued);
        Save(p);
        return issued;
    }

    /// <summary>Re-creates selected formats from the stored PEM material into another folder.</summary>
    public static List<string> Export(CertificateProfile p, IssuedCertificate i, string targetFolder, IEnumerable<string> suffixes, string? pfxPassword)
    {
        var (leaf, issuers, key) = ReadMaterial(p, i);
        Directory.CreateDirectory(targetFolder);
        return WriteFiles(targetFolder, BaseFileName(p), leaf, issuers, key, pfxPassword, p.Name, suffixes);
    }

    public static (string Leaf, List<string> Issuers, string Key) ReadMaterial(CertificateProfile p, IssuedCertificate i)
    {
        var folder = IssuanceFolder(p, i);
        var b = BaseFileName(p);
        var leaf = File.ReadAllText(Path.Combine(folder, b + CertFileKind.Cer));
        var key = File.ReadAllText(Path.Combine(folder, b + CertFileKind.Key));
        var chainFile = Path.Combine(folder, b + CertFileKind.Chain);
        var issuers = File.Exists(chainFile) ? SplitPem(File.ReadAllText(chainFile)) : new List<string>();
        return (leaf, issuers, key);
    }

    public static byte[] BuildPfx(string leafPem, IEnumerable<string> issuerPems, string keyPem, string? password, string friendlyName)
    {
        password ??= "";
        var legacy = SettingsService.Current.PfxLegacyEncryption;
        var pbe = legacy
            ? new PbeParameters(PbeEncryptionAlgorithm.TripleDes3KeyPkcs12, HashAlgorithmName.SHA1, 2000)
            : new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 2000);

        using var leaf = X509Certificate2.CreateFromPem(leafPem);
        using AsymmetricAlgorithm key = leaf.PublicKey.Oid.Value == "1.2.840.10045.2.1" ? ECDsa.Create() : RSA.Create();
        key.ImportFromPem(keyPem);

        // Attributes that tie the key to its certificate and give Windows a display name.
        var localKeyId = new Pkcs9LocalKeyId(leaf.GetCertHash());

        var certs = new Pkcs12SafeContents();
        var leafBag = certs.AddCertificate(leaf);
        leafBag.Attributes.Add(localKeyId);
        leafBag.Attributes.Add(new AsnEncodedData(new Oid("1.2.840.113549.1.9.20"), EncodeBmp(friendlyName)));
        foreach (var pem in issuerPems)
        {
            using var ic = X509Certificate2.CreateFromPem(pem);
            certs.AddCertificate(ic);
        }

        var keys = new Pkcs12SafeContents();
        var keyBag = keys.AddShroudedKey(key, password, pbe);
        keyBag.Attributes.Add(localKeyId);
        keyBag.Attributes.Add(new AsnEncodedData(new Oid("1.2.840.113549.1.9.20"), EncodeBmp(friendlyName)));

        var builder = new Pkcs12Builder();
        builder.AddSafeContentsEncrypted(certs, password, pbe);
        builder.AddSafeContentsUnencrypted(keys);
        builder.SealWithMac(password, legacy ? HashAlgorithmName.SHA1 : HashAlgorithmName.SHA256, 2000);
        return builder.Encode();
    }

    private static byte[] EncodeBmp(string text)
    {
        var w = new AsnWriter(AsnEncodingRules.DER);
        w.WriteCharacterString(UniversalTagNumber.BMPString, text);
        return w.Encode();
    }

    /// <summary>Installs the certificate (with key) into a Windows certificate store; intermediates go to the CA store.</summary>
    public static void InstallToWindowsStore(CertificateProfile p, IssuedCertificate i, StoreLocation location)
    {
        var (leafPem, issuers, key) = ReadMaterial(p, i);
        var password = Guid.NewGuid().ToString("N");
        var pfx = BuildPfx(leafPem, Array.Empty<string>(), key, password, p.Name);
        var flags = X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable |
                    (location == StoreLocation.LocalMachine ? X509KeyStorageFlags.MachineKeySet : X509KeyStorageFlags.UserKeySet);
        using var cert = X509CertificateLoader.LoadPkcs12(pfx, password, flags);
        try { cert.FriendlyName = $"{p.Name} ({i.NotAfter.ToLocalTime():yyyy-MM-dd})"; } catch { }

        using (var my = new X509Store(StoreName.My, location))
        {
            my.Open(OpenFlags.ReadWrite);
            my.Add(cert);
        }
        using (var ca = new X509Store(StoreName.CertificateAuthority, location))
        {
            ca.Open(OpenFlags.ReadWrite);
            foreach (var pem in issuers)
            {
                using var ic = X509Certificate2.CreateFromPem(pem);
                ca.Add(ic);
            }
        }
    }

    private static List<string> WriteFiles(string folder, string b, string leafPem, IList<string> issuerPems, string keyPem,
        string? pfxPassword, string friendlyName, IEnumerable<string> suffixes)
    {
        leafPem = Normalize(leafPem);
        keyPem = Normalize(keyPem);
        var chainPem = string.Concat(issuerPems.Select(Normalize));
        var fullChain = leafPem + chainPem;
        var written = new List<string>();
        var set = suffixes.ToHashSet();

        void Put(string suffix, Action<string> write)
        {
            if (!set.Contains(suffix)) return;
            var path = Path.Combine(folder, b + suffix);
            write(path);
            written.Add(Path.GetFileName(path));
        }

        var utf8 = new UTF8Encoding(false);
        Put(CertFileKind.Cer, f => File.WriteAllText(f, leafPem, utf8));
        Put(CertFileKind.Key, f => File.WriteAllText(f, keyPem, utf8));
        Put(CertFileKind.FullChain, f => File.WriteAllText(f, fullChain, utf8));
        Put(CertFileKind.Combined, f => File.WriteAllText(f, fullChain + keyPem, utf8));
        Put(CertFileKind.Chain, f => File.WriteAllText(f, chainPem, utf8));
        Put(CertFileKind.DerCer, f =>
        {
            using var c = X509Certificate2.CreateFromPem(leafPem);
            File.WriteAllBytes(f, c.RawData);
        });
        Put(CertFileKind.Pfx, f => File.WriteAllBytes(f, BuildPfx(leafPem, issuerPems, keyPem, pfxPassword, friendlyName)));
        return written;
    }

    private static string Normalize(string pem)
    {
        pem = pem.Replace("\r\n", "\n").Trim() + "\n";
        return pem;
    }

    public static List<string> SplitPem(string text)
    {
        var list = new List<string>();
        const string end = "-----END CERTIFICATE-----";
        var idx = 0;
        while (true)
        {
            var start = text.IndexOf("-----BEGIN CERTIFICATE-----", idx, StringComparison.Ordinal);
            if (start < 0) break;
            var stop = text.IndexOf(end, start, StringComparison.Ordinal);
            if (stop < 0) break;
            list.Add(text.Substring(start, stop + end.Length - start) + "\n");
            idx = stop + end.Length;
        }
        return list;
    }

    /// <summary>Creates a new private key and returns it with its PEM text.</summary>
    public static (AsymmetricAlgorithm Key, string Pem) NewPrivateKey(CertKeyType type)
    {
        var pkcs8 = SettingsService.Current.KeyFormatPkcs8;
        switch (type)
        {
            case CertKeyType.EcdsaP256:
            case CertKeyType.EcdsaP384:
                var ec = ECDsa.Create(type == CertKeyType.EcdsaP256 ? ECCurve.NamedCurves.nistP256 : ECCurve.NamedCurves.nistP384);
                return (ec, pkcs8 ? ec.ExportPkcs8PrivateKeyPem() : ec.ExportECPrivateKeyPem());
            default:
                var size = type switch { CertKeyType.Rsa3072 => 3072, CertKeyType.Rsa4096 => 4096, _ => 2048 };
                var rsa = RSA.Create(size);
                return (rsa, pkcs8 ? rsa.ExportPkcs8PrivateKeyPem() : rsa.ExportRSAPrivateKeyPem());
        }
    }

    /// <summary>Builds a DER CSR with every domain as a SAN.</summary>
    public static byte[] CreateCsr(AsymmetricAlgorithm key, IList<string> domains)
    {
        // CN is limited to 64 characters; Let's Encrypt accepts an empty subject when every name is longer.
        var cn = domains.FirstOrDefault(d => d.Length <= 64);
        var subject = new X500DistinguishedName(cn == null ? "" : "CN=" + cn);
        CertificateRequest req = key switch
        {
            RSA rsa => new CertificateRequest(subject, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1),
            ECDsa ec => new CertificateRequest(subject, ec, ec.KeySize > 256 ? HashAlgorithmName.SHA384 : HashAlgorithmName.SHA256),
            _ => throw new NotSupportedException("Unsupported key type")
        };
        var san = new SubjectAlternativeNameBuilder();
        foreach (var d in domains) san.AddDnsName(d);
        req.CertificateExtensions.Add(san.Build());
        return req.CreateSigningRequest();
    }
}
