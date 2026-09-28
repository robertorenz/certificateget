using System.Net.Sockets;
using System.Text.Json;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using CertificateGet.Models;
using Directory = System.IO.Directory;

namespace CertificateGet.Services;

/// <summary>What the issuing workflow needs from the UI while it runs.</summary>
public interface IIssueUi
{
    /// <summary>Show the TXT records the user must create; return false to cancel.</summary>
    Task<bool> ConfirmDnsRecordsAsync(IList<DnsTxtRecord> records);
}

public class AcmeAccountRecord
{
    public string Environment { get; set; } = "";
    public string? Email { get; set; }
    public string ProtectedKeyPem { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
}

public class AcmeService
{
    private readonly CertificateProfile _p;
    private readonly Action<LogLevel, string> _report;

    public AcmeService(CertificateProfile profile, Action<LogLevel, string> report)
    {
        _p = profile;
        _report = report;
    }

    private void Step(string message, LogLevel level = LogLevel.Info)
    {
        ActivityLog.Write(level, "Issue", message, _p.Name);
        _report(level, message);
    }

    public static Uri DirectoryFor(CertificateProfile p) =>
        p.Authority == CertificateAuthority.ZeroSsl ? ZeroSsl.Directory
        : p.Environment == AcmeEnvironment.Production ? WellKnownServers.LetsEncryptV2 : WellKnownServers.LetsEncryptStagingV2;

    /// <summary>"Let's Encrypt Staging", "Let's Encrypt Production" or "ZeroSSL", for messages.</summary>
    private string Ca => _p.Authority == CertificateAuthority.ZeroSsl ? "ZeroSSL" : "Let's Encrypt " + _p.EnvironmentDisplay;

    /// <summary>One stored ACME account per server: staging.json and production.json (Let's Encrypt), zerossl.json.</summary>
    public static string AccountFileName(CertificateProfile p) =>
        p.Authority == CertificateAuthority.ZeroSsl ? "zerossl.json" : $"{p.Environment.ToString().ToLowerInvariant()}.json";

    private async Task<IAcmeContext> GetAccountAsync()
    {
        SettingsService.EnsureStore();
        var file = Path.Combine(SettingsService.AccountsPath, AccountFileName(_p));
        if (File.Exists(file))
        {
            var rec = JsonSerializer.Deserialize<AcmeAccountRecord>(File.ReadAllText(file), Json.Options);
            var pem = Secret.Unprotect(rec?.ProtectedKeyPem);
            if (pem != null)
            {
                Step($"Using existing {Ca} account.");
                return new AcmeContext(DirectoryFor(_p), KeyFactory.FromPem(pem));
            }
            Step("Stored account key could not be decrypted (different Windows user?) — creating a new account.", LogLevel.Warning);
        }

        Step($"Creating a new {Ca} account…");
        var key = KeyFactory.NewKey(KeyAlgorithm.ES256);
        var ctx = new AcmeContext(DirectoryFor(_p), key);
        var email = string.IsNullOrWhiteSpace(_p.Email) ? SettingsService.Current.DefaultEmail : _p.Email;
        if (_p.Authority == CertificateAuthority.ZeroSsl)
        {
            // ZeroSSL only accepts accounts bound to a ZeroSSL account (External Account Binding).
            var apiKey = Secret.Unprotect(SettingsService.Current.ProtectedZeroSslApiKey);
            Step(string.IsNullOrWhiteSpace(apiKey)
                ? $"Requesting ZeroSSL EAB credentials for {email}…"
                : "Requesting ZeroSSL EAB credentials with the API key from Settings…");
            var eab = await ZeroSsl.GetEabAsync(apiKey, email);
            var contact = string.IsNullOrWhiteSpace(email) ? new List<string>() : new List<string> { "mailto:" + email.Trim() };
            await ctx.NewAccount(contact, true, eab.KeyId, eab.HmacKey, "HS256");
        }
        else try
        {
            var contact = string.IsNullOrWhiteSpace(email) ? new List<string>() : new List<string> { "mailto:" + email.Trim() };
            await ctx.NewAccount(contact, true, null, null, null);
        }
        catch (AcmeRequestException ex) when (!string.IsNullOrWhiteSpace(email))
        {
            Step($"Account with contact e-mail was rejected ({Describe(ex)}); retrying without e-mail.", LogLevel.Warning);
            await ctx.NewAccount(new List<string>(), true, null, null, null);
        }

        var record = new AcmeAccountRecord
        {
            Environment = _p.Authority == CertificateAuthority.ZeroSsl ? "ZeroSSL" : _p.Environment.ToString(),
            Email = email,
            ProtectedKeyPem = Secret.Protect(key.ToPem())!,
            CreatedUtc = DateTime.UtcNow
        };
        File.WriteAllText(file, JsonSerializer.Serialize(record, Json.Options));
        ActivityLog.Success("Account", $"Created {Ca} account{(string.IsNullOrWhiteSpace(email) ? "" : " for " + email)}.");
        return ctx;
    }

    public static string Describe(Exception ex) => ex switch
    {
        AcmeRequestException are when are.Error?.Detail != null => are.Error.Detail,
        _ => ex.Message
    };

    private record PendingChallenge(IAuthorizationContext Authz, IChallengeContext Challenge, string Domain, string Display);

    public async Task<IssuedCertificate> IssueAsync(string? pfxPassword, IIssueUi ui, CancellationToken ct)
    {
        var isDns = _p.Challenge is ChallengeMethod.DnsManual or ChallengeMethod.DnsCloudflare or ChallengeMethod.DnsHostinger or ChallengeMethod.DnsConstellix
            or ChallengeMethod.DnsAcmeDns or ChallengeMethod.DnsMadeEasy or ChallengeMethod.DnsNamecheap;
        if (_p.IsWildcard && !isDns)
            throw new InvalidOperationException("Wildcard certificates can only be validated with a DNS challenge.");

        Step($"Requesting certificate for {_p.DomainsDisplay} from {Ca}.");
        var ctx = await GetAccountAsync();
        ct.ThrowIfCancellationRequested();

        var order = await ctx.NewOrder(_p.Domains);
        Step("Order created. Fetching authorizations…");

        var pending = new List<PendingChallenge>();
        foreach (var authz in await order.Authorizations())
        {
            var res = await authz.Resource();
            var domain = res.Identifier.Value;
            var display = res.Wildcard == true ? "*." + domain : domain;
            if (res.Status == AuthorizationStatus.Valid)
            {
                Step($"{display}: already validated recently, no challenge needed.");
                continue;
            }
            var ch = isDns ? await authz.Dns() : await authz.Http();
            if (ch == null) throw new InvalidOperationException($"{_p.AuthorityDisplay} offered no {(isDns ? "DNS" : "HTTP")} challenge for {display}.");
            pending.Add(new PendingChallenge(authz, ch, domain, display));
        }

        if (pending.Count > 0)
        {
            switch (_p.Challenge)
            {
                case ChallengeMethod.HttpSelfHosted: await RunHttpSelfHosted(pending, ct); break;
                case ChallengeMethod.HttpWebRoot: await RunHttpWebRoot(pending, ct); break;
                case ChallengeMethod.DnsManual: await RunDnsManual(ctx, pending, ui, ct); break;
                case ChallengeMethod.DnsCloudflare:
                case ChallengeMethod.DnsHostinger:
                case ChallengeMethod.DnsConstellix:
                case ChallengeMethod.DnsAcmeDns:
                case ChallengeMethod.DnsMadeEasy:
                case ChallengeMethod.DnsNamecheap: await RunDnsProvider(ctx, pending, ui, ct); break;
            }
        }

        await WaitForOrder(order, OrderStatus.Ready, ct);

        Step($"All domains validated. Generating {_p.KeyType} private key and CSR…");
        var (key, keyPem) = CertificateStore.NewPrivateKey(_p.KeyType);
        using (key)
        {
            var csr = CertificateStore.CreateCsr(key, _p.Domains);
            await order.Finalize(csr);
        }
        Step($"CSR submitted. Waiting for {_p.AuthorityDisplay} to issue the certificate…");
        await WaitForOrder(order, OrderStatus.Valid, ct);

        var chain = await order.Download(null);
        var leafPem = chain.Certificate.ToPem();
        var issuers = chain.Issuers.Select(i => i.ToPem()).ToList();

        var issued = CertificateStore.SaveIssuance(_p, leafPem, issuers, keyPem, pfxPassword);
        Step($"Certificate issued by {issued.Issuer}, valid until {issued.NotAfter.ToLocalTime():yyyy-MM-dd HH:mm}. Saved {issued.Files.Count} files.", LogLevel.Success);
        return issued;
    }

    private async Task WaitForOrder(IOrderContext order, OrderStatus wanted, CancellationToken ct)
    {
        // ZeroSSL can keep an order "processing" for several minutes before the certificate is ready.
        var tries = _p.Authority == CertificateAuthority.ZeroSsl ? 300 : 60;
        for (var i = 0; i < tries; i++)
        {
            var o = await order.Resource();
            if (o.Status == wanted || o.Status == OrderStatus.Valid) return;
            if (o.Status == OrderStatus.Invalid)
                throw new InvalidOperationException($"{_p.AuthorityDisplay} marked the order invalid. Check the activity log for challenge errors.");
            await Task.Delay(2000, ct);
        }
        throw new TimeoutException($"Timed out waiting for the order to become {wanted}.");
    }

    private async Task ValidateAll(List<PendingChallenge> pending, CancellationToken ct)
    {
        foreach (var pc in pending)
        {
            Step($"{pc.Display}: asking {_p.AuthorityDisplay} to validate…");
            await pc.Challenge.Validate();
        }

        foreach (var pc in pending)
        {
            for (var i = 0; ; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(i == 0 ? 1500 : 3000, ct);
                var r = await pc.Challenge.Resource();
                if (r.Status == ChallengeStatus.Valid)
                {
                    Step($"{pc.Display}: validated ✔", LogLevel.Success);
                    break;
                }
                if (r.Status == ChallengeStatus.Invalid)
                    throw new InvalidOperationException($"{pc.Display}: validation failed — {r.Error?.Detail ?? "no detail given"}");
                if (i > 60) throw new TimeoutException($"{pc.Display}: validation timed out.");
            }
        }
    }

    private async Task RunHttpSelfHosted(List<PendingChallenge> pending, CancellationToken ct)
    {
        using var server = new Http01Server();
        foreach (var pc in pending) server.AddToken(pc.Challenge.Token, pc.Challenge.KeyAuthz);
        try
        {
            server.Start(_p.HttpPort);
        }
        catch (SocketException ex)
        {
            throw new InvalidOperationException(
                $"Could not listen on port {_p.HttpPort}: {ex.Message}. If IIS or another web server is using the port, use the \"HTTP (web root)\" method instead.");
        }
        Step($"Built-in HTTP server listening on port {_p.HttpPort}. {_p.AuthorityDisplay} must reach http://<domain>/.well-known/acme-challenge/ on this machine.");
        await ValidateAll(pending, ct);
    }

    private async Task RunHttpWebRoot(List<PendingChallenge> pending, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_p.WebRootPath) || !Directory.Exists(_p.WebRootPath!))
            throw new InvalidOperationException($"Web root folder not found: {_p.WebRootPath}");

        var dir = Path.Combine(_p.WebRootPath, ".well-known", "acme-challenge");
        Directory.CreateDirectory(dir);
        var written = new List<string>();
        // IIS will not serve extension-less files without a MIME map.
        var webConfig = Path.Combine(dir, "web.config");
        if (!File.Exists(webConfig))
        {
            File.WriteAllText(webConfig,
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<configuration>\n  <system.webServer>\n    <staticContent>\n" +
                "      <remove fileExtension=\".\" />\n      <mimeMap fileExtension=\".\" mimeType=\"text/plain\" />\n" +
                "    </staticContent>\n    <handlers>\n      <clear />\n      <add name=\"StaticFile\" path=\"*\" verb=\"*\" modules=\"StaticFileModule\" resourceType=\"Either\" requireAccess=\"Read\" />\n" +
                "    </handlers>\n  </system.webServer>\n</configuration>\n");
            written.Add(webConfig);
        }
        try
        {
            foreach (var pc in pending)
            {
                var file = Path.Combine(dir, pc.Challenge.Token);
                await File.WriteAllTextAsync(file, pc.Challenge.KeyAuthz, ct);
                written.Add(file);
                Step($"{pc.Display}: wrote challenge file {file}");
            }
            await ValidateAll(pending, ct);
        }
        finally
        {
            foreach (var f in written)
                try { File.Delete(f); } catch { }
            Step("Removed challenge files from the web root.");
        }
    }

    private List<DnsTxtRecord> BuildDnsRecords(IAcmeContext ctx, List<PendingChallenge> pending) =>
        pending.Select(pc => new DnsTxtRecord
        {
            Domain = pc.Display,
            RecordName = "_acme-challenge." + pc.Domain,
            Value = ctx.AccountKey.DnsTxt(pc.Challenge.Token)
        }).ToList();

    private async Task RunDnsManual(IAcmeContext ctx, List<PendingChallenge> pending, IIssueUi ui, CancellationToken ct)
    {
        var records = BuildDnsRecords(ctx, pending);
        foreach (var r in records) Step($"DNS TXT needed: {r.RecordName} = {r.Value}");
        if (!await ui.ConfirmDnsRecordsAsync(records))
            throw new OperationCanceledException("Cancelled while waiting for DNS records.");
        await DnsChecker.AllVisibleAsync(records);
        foreach (var r in records.Where(r => !r.Found))
            Step($"{r.RecordName} is not visible yet on public resolvers — validating anyway.", LogLevel.Warning);
        await ValidateAll(pending, ct);
        Step("You can now delete the _acme-challenge TXT records from your DNS.");
    }

    private static IDnsProvider CreateDnsProvider(ChallengeMethod method)
    {
        var s = SettingsService.Current;
        return method switch
        {
            ChallengeMethod.DnsCloudflare => new CloudflareDns(Secret.Unprotect(s.ProtectedCloudflareToken)
                ?? throw new InvalidOperationException("No Cloudflare API token configured. Add one in Settings.")),
            ChallengeMethod.DnsHostinger => new HostingerDns(Secret.Unprotect(s.ProtectedHostingerToken)
                ?? throw new InvalidOperationException("No Hostinger API token configured. Add one in Settings.")),
            ChallengeMethod.DnsConstellix => new ConstellixDns(
                Secret.Unprotect(s.ProtectedConstellixApiKey) ?? throw new InvalidOperationException("No Constellix API key configured. Add it in Settings."),
                Secret.Unprotect(s.ProtectedConstellixSecretKey) ?? throw new InvalidOperationException("No Constellix secret key configured. Add it in Settings.")),
            ChallengeMethod.DnsMadeEasy => new DnsMadeEasyDns(
                Secret.Unprotect(s.ProtectedDnsMadeEasyApiKey) ?? throw new InvalidOperationException("No DNS Made Easy API key configured. Add it in Settings."),
                Secret.Unprotect(s.ProtectedDnsMadeEasySecretKey) ?? throw new InvalidOperationException("No DNS Made Easy secret key configured. Add it in Settings.")),
            ChallengeMethod.DnsNamecheap => new NamecheapDns(
                string.IsNullOrWhiteSpace(s.NamecheapApiUser) ? throw new InvalidOperationException("No Namecheap API user configured. Add it in Settings.") : s.NamecheapApiUser,
                Secret.Unprotect(s.ProtectedNamecheapApiKey) ?? throw new InvalidOperationException("No Namecheap API key configured. Add it in Settings."),
                s.NamecheapClientIp),
            ChallengeMethod.DnsAcmeDns => new AcmeDnsProvider(s.AcmeDnsAccounts.ToList()),
            _ => throw new NotSupportedException($"{method} is not an automatic DNS provider.")
        };
    }

    /// <summary>acme-dns: registers every domain that has no account yet, then makes sure each
    /// _acme-challenge CNAME points at its acme-dns name, asking the user to create the missing ones.</summary>
    private async Task PrepareAcmeDns(List<DnsTxtRecord> records, IIssueUi ui, CancellationToken ct)
    {
        var s = SettingsService.Current;
        var accounts = new List<AcmeDnsAccount>();
        foreach (var domain in records.Select(r => AcmeDnsProvider.BaseDomain(r.RecordName)).Distinct())
        {
            var account = s.AcmeDnsAccounts.FirstOrDefault(a => a.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase));
            if (account == null)
            {
                Step($"acme-dns: registering {domain} on {AcmeDnsProvider.NormalizeServer(s.AcmeDnsServer)}…");
                var (created, password) = await AcmeDnsProvider.RegisterAsync(s.AcmeDnsServer, domain);
                created.ProtectedPassword = Secret.Protect(password);
                s.AcmeDnsAccounts.Add(created);
                SettingsService.Save(s);
                account = created;
            }
            accounts.Add(account);
        }

        var cnames = accounts.Select(a => new DnsTxtRecord { Type = "CNAME", Domain = a.Domain, RecordName = a.CnameName, Value = a.FullDomain }).ToList();
        if (await DnsChecker.AllVisibleAsync(cnames))
        {
            Step("acme-dns: every _acme-challenge CNAME is in place.", LogLevel.Success);
            return;
        }
        foreach (var c in cnames.Where(c => !c.Found))
            Step($"acme-dns: CNAME needed (one time): {c.RecordName} → {c.Value}", LogLevel.Warning);
        if (!await ui.ConfirmDnsRecordsAsync(cnames))
            throw new OperationCanceledException("Cancelled while waiting for the acme-dns CNAME records.");
        ct.ThrowIfCancellationRequested();
    }

    private async Task RunDnsProvider(IAcmeContext ctx, List<PendingChallenge> pending, IIssueUi ui, CancellationToken ct)
    {
        var records = BuildDnsRecords(ctx, pending);
        if (_p.Challenge == ChallengeMethod.DnsAcmeDns) await PrepareAcmeDns(records, ui, ct);
        using var dns = CreateDnsProvider(_p.Challenge);
        var added = false;
        try
        {
            await dns.AddTxtRecordsAsync(records);
            added = true;
            foreach (var r in records) Step($"{dns.DisplayName}: created TXT {r.RecordName}");
            await WaitForPropagation(records, ct);
            await ValidateAll(pending, ct);
        }
        finally
        {
            // Also runs after a partial add, so any record that did get created is removed.
            await dns.CleanupAsync();
            if (added && _p.Challenge != ChallengeMethod.DnsAcmeDns) Step($"{dns.DisplayName}: removed challenge TXT records.");
        }
    }

    private async Task WaitForPropagation(List<DnsTxtRecord> records, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(SettingsService.Current.DnsPropagationTimeoutSeconds);
        Step("Waiting for DNS propagation…");
        while (true)
        {
            if (await DnsChecker.AllVisibleAsync(records))
            {
                Step("DNS records are visible on public resolvers.", LogLevel.Success);
                // Give secondary name servers a moment too.
                await Task.Delay(10000, ct);
                return;
            }
            if (DateTime.UtcNow > deadline)
            {
                Step("DNS propagation timeout reached — validating anyway.", LogLevel.Warning);
                return;
            }
            await Task.Delay(10000, ct);
        }
    }
}
