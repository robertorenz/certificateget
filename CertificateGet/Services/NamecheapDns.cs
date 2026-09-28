using System.Net.Http.Headers;
using System.Xml.Linq;
using CertificateGet.Models;

namespace CertificateGet.Services;

/// <summary>
/// Namecheap XML API client (https://api.namecheap.com/xml.response) for the _acme-challenge TXT records.
/// Namecheap has no call to add a single record: namecheap.domains.dns.setHosts replaces the whole host list.
/// So every change reads the current list with getHosts, adds or removes only our TXT values, and writes
/// every other record back exactly as it was (name, type, address, MX preference, TTL, and the email type).
/// Requirements on Namecheap's side: API access enabled, this PC's public IP whitelisted, and the domain on
/// Namecheap BasicDNS / PremiumDNS.
/// </summary>
public sealed class NamecheapDns : IDnsProvider
{
    private const string Ttl = "60";
    private static readonly XNamespace Ns = "http://api.namecheap.com/xml.response";
    private readonly HttpClient _http;
    private readonly string _apiUser, _apiKey;
    private readonly string? _clientIp;
    private string? _resolvedIp;
    private List<string>? _domains;
    private readonly List<(string Domain, string Host, HashSet<string> Values)> _added = new();

    public string DisplayName => "Namecheap";

    /// <param name="clientIp">This PC's public IPv4 as whitelisted at Namecheap; null = detect it.</param>
    /// <param name="handler">Optional HTTP handler (used by tests to simulate the API).</param>
    public NamecheapDns(string apiUser, string apiKey, string? clientIp, HttpMessageHandler? handler = null)
    {
        _apiUser = apiUser.Trim();
        _apiKey = apiKey.Trim();
        _clientIp = string.IsNullOrWhiteSpace(clientIp) ? null : clientIp.Trim();
        _http = new HttpClient(handler ?? new HttpClientHandler()) { Timeout = TimeSpan.FromSeconds(60) };
    }

    private record Host(string Name, string Type, string Address, string MxPref, string Ttl);

    /// <summary>The public IP sent as ClientIp: the one from Settings, or the one this PC is seen with.</summary>
    public async Task<string> ClientIpAsync()
    {
        if (_clientIp != null) return _clientIp;
        if (_resolvedIp != null) return _resolvedIp;
        try
        {
            _resolvedIp = (await _http.GetStringAsync("https://api.ipify.org")).Trim();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Could not detect this PC's public IP ({ex.Message}). Enter it in Settings → Namecheap client IP.");
        }
        return _resolvedIp;
    }

    /// <summary>Domain names in the account (used by the "Test" button and to split names into SLD + TLD).</summary>
    public async Task<List<string>> ListDomainsAsync()
    {
        if (_domains != null) return _domains;
        var names = new List<string>();
        for (var page = 1; page <= 50; page++)
        {
            var resp = await Call("namecheap.domains.getList", new() { ["PageSize"] = "100", ["Page"] = page.ToString() });
            var list = resp.Descendants(Ns + "Domain").Select(d => (string?)d.Attribute("Name")).Where(n => !string.IsNullOrEmpty(n)).ToList();
            names.AddRange(list.Select(n => n!.ToLowerInvariant()));
            var paging = resp.Descendants(Ns + "Paging").FirstOrDefault();
            var total = int.TryParse(paging?.Element(Ns + "TotalItems")?.Value, out var t) ? t : names.Count;
            if (list.Count == 0 || names.Count >= total) break;
        }
        return _domains = names;
    }

    /// <summary>"_acme-challenge.shop.example.co.uk" → ("example.co.uk", "_acme-challenge.shop").</summary>
    private async Task<(string Domain, string Host)> SplitAsync(string recordName)
    {
        var fqdn = recordName.TrimEnd('.').ToLowerInvariant();
        var domain = (await ListDomainsAsync())
            .Where(d => fqdn.EndsWith("." + d))
            .OrderByDescending(d => d.Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"No Namecheap domain in this account matches {recordName}.");
        return (domain, fqdn[..^(domain.Length + 1)]);
    }

    private static (string Sld, string Tld) SldTld(string domain)
    {
        var dot = domain.IndexOf('.');
        return (domain[..dot], domain[(dot + 1)..]);
    }

    private async Task<(List<Host> Hosts, string? EmailType)> GetHostsAsync(string domain)
    {
        var (sld, tld) = SldTld(domain);
        var resp = await Call("namecheap.domains.dns.getHosts", new() { ["SLD"] = sld, ["TLD"] = tld });
        var result = resp.Descendants(Ns + "DomainDNSGetHostsResult").FirstOrDefault()
                     ?? throw new InvalidOperationException($"Namecheap returned no host list for {domain}.");
        if (string.Equals((string?)result.Attribute("IsUsingOurDNS"), "false", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"{domain} does not use Namecheap's DNS servers, so its records cannot be changed through the Namecheap API.");
        var hosts = result.Elements().Where(e => e.Name.LocalName.Equals("host", StringComparison.OrdinalIgnoreCase)).Select(h => new Host(
            (string?)h.Attribute("Name") ?? "",
            (string?)h.Attribute("Type") ?? "",
            (string?)h.Attribute("Address") ?? "",
            (string?)h.Attribute("MXPref") ?? "10",
            (string?)h.Attribute("TTL") ?? "1800")).ToList();
        return (hosts, (string?)result.Attribute("EmailType"));
    }

    private async Task SetHostsAsync(string domain, List<Host> hosts, string? emailType)
    {
        var (sld, tld) = SldTld(domain);
        var p = new Dictionary<string, string> { ["SLD"] = sld, ["TLD"] = tld };
        // Keep the mail setting as it is, or Namecheap may drop custom MX records.
        if (!string.IsNullOrEmpty(emailType)) p["EmailType"] = emailType;
        else if (hosts.Any(h => h.Type.Equals("MX", StringComparison.OrdinalIgnoreCase))) p["EmailType"] = "MX";
        for (var i = 0; i < hosts.Count; i++)
        {
            var n = (i + 1).ToString();
            p["HostName" + n] = hosts[i].Name;
            p["RecordType" + n] = hosts[i].Type;
            p["Address" + n] = hosts[i].Address;
            p["MXPref" + n] = hosts[i].MxPref;
            p["TTL" + n] = hosts[i].Ttl;
        }
        var resp = await Call("namecheap.domains.dns.setHosts", p);
        var ok = resp.Descendants(Ns + "DomainDNSSetHostsResult").FirstOrDefault()?.Attribute("IsSuccess")?.Value;
        if (!string.Equals(ok, "true", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Namecheap did not confirm the update of {domain}.");
    }

    public async Task AddTxtRecordsAsync(IList<DnsTxtRecord> records)
    {
        var split = new List<(string Domain, string Host, string Value)>();
        foreach (var r in records)
        {
            var (domain, host) = await SplitAsync(r.RecordName);
            split.Add((domain, host, r.Value));
        }
        foreach (var byDomain in split.GroupBy(s => s.Domain))
        {
            var (hosts, emailType) = await GetHostsAsync(byDomain.Key);
            var before = hosts.Count;
            foreach (var byHost in byDomain.GroupBy(s => s.Host))
            {
                var ours = byHost.Select(s => s.Value).ToHashSet();
                foreach (var v in ours.Where(v => !hosts.Any(h => h.Type == "TXT" && h.Name.Equals(byHost.Key, StringComparison.OrdinalIgnoreCase) && h.Address == v)))
                    hosts.Add(new Host(byHost.Key, "TXT", v, "10", Ttl));
                _added.Add((byDomain.Key, byHost.Key, ours));
            }
            await SetHostsAsync(byDomain.Key, hosts, emailType);
            ActivityLog.Info("Namecheap", $"Added {hosts.Count - before} TXT value(s) to {byDomain.Key}; the other {before} record(s) were written back unchanged.");
        }
    }

    public async Task CleanupAsync()
    {
        foreach (var byDomain in _added.GroupBy(a => a.Domain).ToList())
        {
            try
            {
                var (hosts, emailType) = await GetHostsAsync(byDomain.Key);
                var removed = hosts.RemoveAll(h => h.Type == "TXT" && byDomain.Any(a =>
                    a.Host.Equals(h.Name, StringComparison.OrdinalIgnoreCase) && a.Values.Contains(h.Address)));
                if (removed > 0) await SetHostsAsync(byDomain.Key, hosts, emailType);
                _added.RemoveAll(a => a.Domain == byDomain.Key);
            }
            catch (Exception ex)
            {
                ActivityLog.Warning("Namecheap", $"Could not remove the _acme-challenge TXT values from {byDomain.Key}: {ex.Message} — delete them in Domain List → Manage → Advanced DNS.");
            }
        }
    }

    private async Task<XElement> Call(string command, Dictionary<string, string> extra)
    {
        var form = new Dictionary<string, string>
        {
            ["ApiUser"] = _apiUser, ["ApiKey"] = _apiKey, ["UserName"] = _apiUser,
            ["ClientIp"] = await ClientIpAsync(), ["Command"] = command
        };
        foreach (var kv in extra) form[kv.Key] = kv.Value;
        using var content = new FormUrlEncodedContent(form);
        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.namecheap.com/xml.response") { Content = content };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/xml"));
        using var resp = await _http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        XElement root;
        try { root = XElement.Parse(text); }
        catch { throw new InvalidOperationException($"Namecheap API error: HTTP {(int)resp.StatusCode}, not an XML answer."); }
        if (!string.Equals((string?)root.Attribute("Status"), "OK", StringComparison.OrdinalIgnoreCase))
        {
            var errors = root.Descendants(Ns + "Error").Select(e => ((string?)e.Attribute("Number"), e.Value.Trim())).ToList();
            var msg = errors.Count == 0 ? "unknown error" : string.Join("; ", errors.Select(e => $"{e.Item2} ({e.Item1})"));
            if (errors.Any(e => e.Item1 is "1011150" or "1011102" or "1010102" or "1011104") ||
                msg.Contains("IP", StringComparison.OrdinalIgnoreCase) && msg.Contains("invalid", StringComparison.OrdinalIgnoreCase))
                msg += $". Check the API user and key, and that {await ClientIpAsync()} is whitelisted in Namecheap → Profile → Tools → API Access.";
            throw new InvalidOperationException($"Namecheap API error: {msg}");
        }
        return root;
    }

    public void Dispose() => _http.Dispose();
}
