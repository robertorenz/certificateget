using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using CertificateGet.Models;

namespace CertificateGet.Services;

/// <summary>
/// acme-dns client (https://github.com/joohoi/acme-dns). Each domain is registered once on the acme-dns server,
/// and the user points _acme-challenge.&lt;domain&gt; at the returned full domain with a CNAME. After that the
/// app only updates the TXT value on the acme-dns server and never needs access to the real DNS.
/// acme-dns keeps the two most recent values per registration, which covers a wildcard plus its bare domain;
/// old values are simply replaced by the next update, so there is nothing to clean up.
/// </summary>
public sealed class AcmeDnsProvider : IDnsProvider
{
    private readonly HttpClient _http;
    private readonly IReadOnlyList<AcmeDnsAccount> _accounts;

    public string DisplayName => "acme-dns";

    /// <param name="accounts">The registrations to use, one per base domain.</param>
    public AcmeDnsProvider(IReadOnlyList<AcmeDnsAccount> accounts, HttpMessageHandler? handler = null)
    {
        _accounts = accounts;
        _http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>The base domain an _acme-challenge record belongs to ("_acme-challenge.example.com" → "example.com").</summary>
    public static string BaseDomain(string recordName) =>
        recordName.TrimEnd('.').ToLowerInvariant() is var n && n.StartsWith("_acme-challenge.") ? n["_acme-challenge.".Length..] : n;

    public static string NormalizeServer(string url)
    {
        url = url.Trim().TrimEnd('/');
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            url = "https://" + url;
        return url;
    }

    /// <summary>Registers a new account on the server. The password is returned in plain text; protect it before storing.</summary>
    public static async Task<(AcmeDnsAccount Account, string Password)> RegisterAsync(string server, string domain, HttpMessageHandler? handler = null)
    {
        server = NormalizeServer(server);
        using var http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromSeconds(30) };
        using var resp = await http.PostAsync(server + "/register", JsonContent.Create(new { }));
        var json = await ReadJson(resp, $"acme-dns registration on {server}");
        var account = new AcmeDnsAccount
        {
            Domain = domain.ToLowerInvariant(),
            Server = server,
            Username = json?["username"]?.ToString() ?? "",
            Subdomain = json?["subdomain"]?.ToString() ?? "",
            FullDomain = (json?["fulldomain"]?.ToString() ?? "").TrimEnd('.'),
        };
        var password = json?["password"]?.ToString() ?? "";
        if (account.Username.Length == 0 || password.Length == 0 || account.Subdomain.Length == 0 || account.FullDomain.Length == 0)
            throw new InvalidOperationException($"acme-dns on {server} returned an incomplete registration.");
        ActivityLog.Info("acme-dns", $"Registered {account.Domain} on {server}: CNAME {account.CnameName} → {account.FullDomain}");
        return (account, password);
    }

    /// <summary>Checks that the server answers (GET /health).</summary>
    public static async Task CheckHealthAsync(string server)
    {
        server = NormalizeServer(server);
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        using var resp = await http.GetAsync(server + "/health");
        if (!resp.IsSuccessStatusCode)
            throw new InvalidOperationException($"{server}/health answered HTTP {(int)resp.StatusCode}. Check the server URL.");
    }

    public async Task AddTxtRecordsAsync(IList<DnsTxtRecord> records)
    {
        foreach (var r in records)
        {
            var domain = BaseDomain(r.RecordName);
            var account = _accounts.FirstOrDefault(a => a.Domain.Equals(domain, StringComparison.OrdinalIgnoreCase))
                          ?? throw new InvalidOperationException($"No acme-dns registration for {domain}.");
            var password = Secret.Unprotect(account.ProtectedPassword)
                           ?? throw new InvalidOperationException($"The acme-dns password for {domain} cannot be read (it was saved by another Windows user). Remove the registration in Settings and request again.");
            using var req = new HttpRequestMessage(HttpMethod.Post, account.Server + "/update")
            {
                Content = JsonContent.Create(new { subdomain = account.Subdomain, txt = r.Value })
            };
            req.Headers.Add("X-Api-User", account.Username);
            req.Headers.Add("X-Api-Key", password);
            using var resp = await _http.SendAsync(req);
            await ReadJson(resp, $"acme-dns update for {domain}");
            ActivityLog.Info("acme-dns", $"Updated TXT for {r.RecordName} via {account.FullDomain}.");
        }
    }

    /// <summary>Nothing to remove: acme-dns keeps only the last two values and the next update replaces them.</summary>
    public Task CleanupAsync() => Task.CompletedTask;

    private static async Task<JsonNode?> ReadJson(HttpResponseMessage resp, string what)
    {
        var text = await resp.Content.ReadAsStringAsync();
        JsonNode? json;
        try { json = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text); } catch { json = null; }
        if (resp.IsSuccessStatusCode) return json;
        var detail = json?["error"]?.ToString() ?? $"HTTP {(int)resp.StatusCode}";
        var hint = detail switch
        {
            "forbidden" or "unauthorized" => " The stored credentials were rejected; remove the registration in Settings and request again.",
            "bad_subdomain" => " The registration no longer exists on the server; remove it in Settings and request again.",
            _ when resp.StatusCode == HttpStatusCode.Unauthorized => " The stored credentials were rejected.",
            _ => ""
        };
        throw new InvalidOperationException($"{what} failed: {detail}.{hint}");
    }

    public void Dispose() => _http.Dispose();
}
