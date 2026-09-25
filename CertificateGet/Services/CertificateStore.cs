using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using CertificateGet.Models;

namespace CertificateGet.Services;

/// <summary>One selectable output format; it may produce one or several files.</summary>
public sealed record CertFormat(
    string Id, string Label, string Description, Func<string, string[]> FileNames,
    bool DefaultOn = false, bool Required = false, bool NeedsPassword = false)
{
    public string FileNamesDisplay(string baseName) => string.Join(", ", FileNames(baseName));
}

/// <summary>All output formats. Required ones are always written: exports and renewals are rebuilt from them.</summary>
public static class CertFileKind
{
    public const string Pfx = ".pfx";
    public const string Cer = ".cer";
    public const string DerCer = "-der.cer";
    public const string Key = ".key";
    public const string Chain = "-chain.cer";
    public const string FullChain = "-fullchain.pem";
    public const string Combined = "-combined.pem";
    public const string P7b = ".p7b";
    public const string Certbot = "certbot";
    public const string EncryptedKey = "-encrypted.key";
    public const string FullChainRoot = "-fullchain-root.pem";
    public const string CombinedKeyFirst = "-combined-keyfirst.pem";
    public const string P12 = ".p12";
    public const string Crt = ".crt";
    public const string Kubernetes = "-k8s-secret.yaml";

    private static CertFormat F(string suffix, string label, string desc, bool on = false, bool required = false, bool pwd = false) =>
        new(suffix, label, desc, b => new[] { b + suffix }, on, required, pwd);

    public static readonly CertFormat[] All =
    {
        F(Pfx, "PFX / PKCS#12", "Certificate + chain + private key in one password-protected file (IIS, Azure, Exchange, Windows).", on: true),
        F(Cer, "CER (PEM)", "The certificate only, Base-64 PEM text.", required: true),
        F(Key, "KEY (PEM)", "The private key, PEM text. Keep it secret.", required: true),
        F(FullChain, "Full chain PEM", "Certificate followed by the intermediate chain (nginx ssl_certificate, Apache 2.4.8+).", on: true),
        F(Combined, "Combined PEM", "Full chain + private key in a single file (HAProxy, Webmin, many appliances).", on: true),
        F(Chain, "Chain (PEM)", "Intermediate certificates only (Apache SSLCertificateChainFile).", required: true),
        F(DerCer, "CER (DER binary)", "The certificate only, binary DER encoding (Java, some Windows tools).", on: true),
        F(P7b, "P7B / PKCS#7", "Certificate + chain, no private key (Windows intermediates, Java keytool, Tomcat, F5, Citrix, Palo Alto)."),
        new(Certbot, "Certbot-style names", "cert.pem, privkey.pem, chain.pem and fullchain.pem — the names Linux guides, Synology, Home Assistant, Proxmox and Docker images expect.",
            _ => new[] { "cert.pem", "privkey.pem", "chain.pem", "fullchain.pem" }),
        F(EncryptedKey, "KEY (encrypted)", "Private key protected with the PFX password, BEGIN ENCRYPTED PRIVATE KEY (FortiGate, Sophos, Cisco, Apache with passphrase).", pwd: true),
        F(FullChainRoot, "Full chain + root", "Certificate + intermediates + the ISRG root, for devices that validate the whole chain on import."),
        F(CombinedKeyFirst, "Combined PEM (key first)", "Private key followed by the full chain (Postfix smtpd_tls_chain_files, lighttpd, Pound)."),
        F(P12, "P12", "Same content as the PFX with a .p12 extension (Java/Tomcat keystores, macOS Keychain, Android)."),
        F(Crt, "CRT (PEM)", "The certificate only, PEM, with the .crt extension Linux and Apache use."),
        F(Kubernetes, "Kubernetes TLS secret", "YAML manifest with tls.crt (full chain) and tls.key, ready for kubectl apply."),
    };

    public static CertFormat Get(string id) => All.First(f => f.Id == id);

    /// <summary>Formats written for new issuances: the user's choice from Settings plus the required ones.</summary>
    public static IEnumerable<string> ForIssuance()
    {
        var chosen = SettingsService.Current.IssueFormats ?? All.Where(f => f.DefaultOn).Select(f => f.Id).ToList();
        return All.Where(f => f.Required || chosen.Contains(f.Id)).Select(f => f.Id);
    }
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

        var (files, notes) = WriteFiles(folder, BaseFileName(p), leafPem, issuerPems, keyPem, pfxPassword, p.Name,
            CertFileKind.ForIssuance());
        issued.Files = files;
        foreach (var n in notes) ActivityLog.Warning("Files", n, p.Name);

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
    public static (List<string> Files, List<string> Notes) Export(CertificateProfile p, IssuedCertificate i, string targetFolder,
        IEnumerable<string> formatIds, string? pfxPassword)
    {
        var (leaf, issuers, key) = ReadMaterial(p, i);
        Directory.CreateDirectory(targetFolder);
        return WriteFiles(targetFolder, BaseFileName(p), leaf, issuers, key, pfxPassword, p.Name, formatIds);
    }

    public static (string Leaf, List<string> Issuers, string Key) ReadMaterial(CertificateProfile p, IssuedCertificate i)
    {
        var folder = IssuanceFolder(p, i);
        var b = BaseFileName(p);
        if (!File.Exists(Path.Combine(folder, b + CertFileKind.Cer)) || !File.Exists(Path.Combine(folder, b + CertFileKind.Key)))
            throw new FileNotFoundException(
                $"The stored files for the issuance of {i.IssuedDisplay} are missing (expected in {folder}). " +
                "They may have been moved or deleted outside the app. Request the certificate again to get new files.");
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

    private static (List<string> Written, List<string> Notes) WriteFiles(string folder, string b, string leafPem, IList<string> issuerPems,
        string keyPem, string? pfxPassword, string friendlyName, IEnumerable<string> formatIds)
    {
        leafPem = Normalize(leafPem);
        keyPem = Normalize(keyPem);
        var chainPem = string.Concat(issuerPems.Select(Normalize));
        var fullChain = leafPem + chainPem;
        var written = new List<string>();
        var notes = new List<string>();
        var set = formatIds.ToHashSet();
        var utf8 = new UTF8Encoding(false);

        void Put(string id, Action<string> write)
        {
            if (!set.Contains(id)) return;
            try
            {
                var path = Path.Combine(folder, b + id);
                write(path);
                written.Add(Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                notes.Add($"{CertFileKind.Get(id).Label} not written: {ex.Message}");
            }
        }

        void Text(string path, string content) => File.WriteAllText(path, content, utf8);

        Put(CertFileKind.Cer, f => Text(f, leafPem));
        Put(CertFileKind.Key, f => Text(f, keyPem));
        Put(CertFileKind.FullChain, f => Text(f, fullChain));
        Put(CertFileKind.Combined, f => Text(f, fullChain + keyPem));
        Put(CertFileKind.Chain, f => Text(f, chainPem));
        Put(CertFileKind.DerCer, f =>
        {
            using var c = X509Certificate2.CreateFromPem(leafPem);
            File.WriteAllBytes(f, c.RawData);
        });
        Put(CertFileKind.Pfx, f => File.WriteAllBytes(f, BuildPfx(leafPem, issuerPems, keyPem, pfxPassword, friendlyName)));
        Put(CertFileKind.P12, f => File.WriteAllBytes(f, BuildPfx(leafPem, issuerPems, keyPem, pfxPassword, friendlyName)));
        Put(CertFileKind.Crt, f => Text(f, leafPem));
        Put(CertFileKind.CombinedKeyFirst, f => Text(f, keyPem + fullChain));
        Put(CertFileKind.P7b, f => File.WriteAllBytes(f, BuildP7b(leafPem, issuerPems)));

        if (set.Contains(CertFileKind.EncryptedKey))
        {
            if (string.IsNullOrEmpty(pfxPassword))
                notes.Add("Encrypted key not written: it needs a PFX password (set one on the certificate or in Export).");
            else
                Put(CertFileKind.EncryptedKey, f => Text(f, Normalize(EncryptKey(leafPem, keyPem, pfxPassword))));
        }

        if (set.Contains(CertFileKind.FullChainRoot))
        {
            var root = FindRootPem(issuerPems);
            if (root == null)
                notes.Add("Full chain + root not written: the root certificate could not be found (not in the Windows root store and could not be downloaded).");
            else
                Put(CertFileKind.FullChainRoot, f => Text(f, fullChain + Normalize(root)));
        }

        if (set.Contains(CertFileKind.Certbot))
        {
            try
            {
                Text(Path.Combine(folder, "cert.pem"), leafPem);
                Text(Path.Combine(folder, "privkey.pem"), keyPem);
                Text(Path.Combine(folder, "chain.pem"), chainPem);
                Text(Path.Combine(folder, "fullchain.pem"), fullChain);
                written.AddRange(new[] { "cert.pem", "privkey.pem", "chain.pem", "fullchain.pem" });
            }
            catch (Exception ex)
            {
                notes.Add($"Certbot-style files not written: {ex.Message}");
            }
        }

        Put(CertFileKind.Kubernetes, f =>
        {
            var name = "tls-" + new string(b.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');
            if (name.Length > 63) name = name[..63].TrimEnd('-');
            var yaml = "apiVersion: v1\nkind: Secret\nmetadata:\n  name: " + name + "\ntype: kubernetes.io/tls\ndata:\n" +
                       "  tls.crt: " + Convert.ToBase64String(utf8.GetBytes(fullChain)) + "\n" +
                       "  tls.key: " + Convert.ToBase64String(utf8.GetBytes(keyPem)) + "\n";
            Text(f, yaml);
        });
        return (written, notes);
    }

    private static byte[] BuildP7b(string leafPem, IEnumerable<string> issuerPems)
    {
        var coll = new X509Certificate2Collection { X509Certificate2.CreateFromPem(leafPem) };
        foreach (var pem in issuerPems) coll.Add(X509Certificate2.CreateFromPem(pem));
        try { return coll.Export(X509ContentType.Pkcs7)!; }
        finally { foreach (var c in coll) c.Dispose(); }
    }

    private static string EncryptKey(string leafPem, string keyPem, string password)
    {
        using var leaf = X509Certificate2.CreateFromPem(leafPem);
        using AsymmetricAlgorithm key = leaf.PublicKey.Oid.Value == "1.2.840.10045.2.1" ? ECDsa.Create() : RSA.Create();
        key.ImportFromPem(keyPem);
        return key.ExportEncryptedPkcs8PrivateKeyPem(password,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 100_000));
    }

    private static readonly Dictionary<string, string?> IssuerCache = new();

    /// <summary>
    /// Returns the certificates above the chain's top intermediate up to a self-signed root (usually just the root;
    /// for cross-signed roots such as ISRG Root X2 it is the cross-signed cert followed by ISRG Root X1).
    /// Looks in the Windows root stores first, then follows the AIA "CA Issuers" URLs. Null if no root is reached.
    /// </summary>
    public static string? FindRootPem(IList<string> issuerPems)
    {
        if (issuerPems.Count == 0) return null;
        var extra = new List<string>();
        var current = issuerPems[^1];
        for (var depth = 0; depth < 4; depth++)
        {
            using var cert = X509Certificate2.CreateFromPem(current);
            if (cert.SubjectName.RawData.AsSpan().SequenceEqual(cert.IssuerName.RawData))
                return extra.Count == 0 ? null : string.Concat(extra.Select(Normalize)); // reached a self-signed root
            var parent = FindIssuerPem(cert);
            if (parent == null) return null;
            extra.Add(parent);
            current = parent;
        }
        return null;
    }

    private static string? FindIssuerPem(X509Certificate2 child)
    {
        var akiHex = child.Extensions.OfType<X509AuthorityKeyIdentifierExtension>().FirstOrDefault()?.KeyIdentifier is { } k
            ? Convert.ToHexString(k.Span) : null;
        var cacheKey = child.Issuer + "|" + akiHex;
        lock (IssuerCache)
            if (IssuerCache.TryGetValue(cacheKey, out var cached)) return cached;

        bool Matches(X509Certificate2 c) =>
            c.SubjectName.RawData.AsSpan().SequenceEqual(child.IssuerName.RawData) &&
            (akiHex == null || string.Equals(
                c.Extensions.OfType<X509SubjectKeyIdentifierExtension>().FirstOrDefault()?.SubjectKeyIdentifier, akiHex,
                StringComparison.OrdinalIgnoreCase));

        string? found = null;
        // A self-signed match in the Windows root stores is the best answer.
        foreach (var loc in new[] { StoreLocation.LocalMachine, StoreLocation.CurrentUser })
        {
            if (found != null) break;
            try
            {
                using var store = new X509Store(StoreName.Root, loc);
                store.Open(OpenFlags.ReadOnly);
                var match = store.Certificates.FirstOrDefault(c => Matches(c) && c.Subject == c.Issuer);
                if (match != null) found = match.ExportCertificatePem();
            }
            catch { /* store not accessible */ }
        }

        if (found == null)
        {
            var aia = child.Extensions.OfType<X509AuthorityInformationAccessExtension>().FirstOrDefault();
            foreach (var uri in aia?.EnumerateCAIssuersUris() ?? Enumerable.Empty<string>())
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
                    var bytes = http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                    using var c = X509CertificateLoader.LoadCertificate(bytes);
                    if (Matches(c)) { found = c.ExportCertificatePem(); break; }
                }
                catch { /* try the next URI */ }
            }
        }

        lock (IssuerCache) IssuerCache[cacheKey] = found;
        return found;
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
