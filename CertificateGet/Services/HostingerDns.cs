using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using CertificateGet.Models;

namespace CertificateGet.Services;

/// <summary>
/// Hostinger DNS API client (https://developers.hostinger.com) for creating/removing _acme-challenge TXT records.
/// Records are appended (overwrite=false) so a wildcard and its bare domain can share one TXT name,
/// and cleanup removes only the values this run added.
/// </summary>
public sealed class HostingerDns : IDnsProvider
{
    private const int Ttl = 300;
    private readonly HttpClient _http;
    private readonly List<(string Zone, string Name, HashSet<string> Values)> _added = new();
    private List<string>? _portfolio;

    public string DisplayName => "Hostinger";

    public HostingerDns(string apiToken)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://developers.hostinger.com/api/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Returns the domain names the token can see (used by the "Test token" button).</summary>
    public async Task<List<string>> ListDomainsAsync()
    {
        var json = await Send(HttpMethod.Get, "domains/v1/portfolio", null);
        return (json as JsonArray ?? new JsonArray())
            .Select(d => d?["domain"]?.GetValue<string>())
            .Where(d => !string.IsNullOrEmpty(d))
            .Select(d => d!.ToLowerInvariant())
            .ToList();
    }

    private async Task<string> FindZoneAsync(string recordName)
    {
        var host = recordName.TrimEnd('.').ToLowerInvariant();
        var labels = host.Split('.');

        // Preferred: match against the account's domain list (longest suffix wins).
        try { _portfolio ??= await ListDomainsAsync(); }
        catch { _portfolio = new List<string>(); }
        var match = _portfolio.Where(d => host.EndsWith("." + d)).OrderByDescending(d => d.Length).FirstOrDefault();
        if (match != null) return match;

        // Fallback: probe the DNS zone endpoint for each parent name.
        for (var i = 1; i < labels.Length - 1; i++)
        {
            var candidate = string.Join('.', labels.Skip(i));
            try
            {
                await Send(HttpMethod.Get, $"dns/v1/zones/{candidate}", null);
                return candidate;
            }
            catch (HostingerApiException ex) when (ex.Status is HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Forbidden)
            {
                // not this one — keep walking up
            }
        }
        throw new InvalidOperationException(
            $"No Hostinger DNS zone found for {recordName}. Check that the domain is in this Hostinger account and uses Hostinger's name servers.");
    }

    private static string Relative(string recordName, string zone) =>
        recordName.TrimEnd('.').ToLowerInvariant()[..^(zone.Length + 1)];

    public async Task AddTxtRecordsAsync(IList<DnsTxtRecord> records)
    {
        foreach (var group in records.GroupBy(r => r.RecordName.ToLowerInvariant()))
        {
            var zone = await FindZoneAsync(group.Key);
            var name = Relative(group.Key, zone);
            var values = group.Select(r => r.Value).ToHashSet();
            var body = new JsonObject
            {
                ["overwrite"] = false,
                ["zone"] = new JsonArray(new JsonObject
                {
                    ["name"] = name,
                    ["type"] = "TXT",
                    ["ttl"] = Ttl,
                    ["records"] = new JsonArray(values.Select(v => (JsonNode)new JsonObject { ["content"] = v }).ToArray())
                })
            };
            await Send(HttpMethod.Put, $"dns/v1/zones/{zone}", body);
            _added.Add((zone, name, values));
            ActivityLog.Info("Hostinger", $"Added TXT {name}.{zone} ({values.Count} value(s)).");
        }
    }

    public async Task CleanupAsync()
    {
        foreach (var (zone, name, values) in _added.ToList())
        {
            try
            {
                // Keep any TXT values at this name that we did not add (e.g. from another tool).
                var current = await Send(HttpMethod.Get, $"dns/v1/zones/{zone}", null) as JsonArray ?? new JsonArray();
                var rrset = current.FirstOrDefault(r =>
                    string.Equals(r?["name"]?.GetValue<string>(), name, StringComparison.OrdinalIgnoreCase) &&
                    r?["type"]?.GetValue<string>() == "TXT");
                var remaining = (rrset?["records"] as JsonArray ?? new JsonArray())
                    .Select(x => x?["content"]?.GetValue<string>() ?? "")
                    .Where(c => c.Length > 0 && !values.Contains(c.Trim('"')))
                    .ToList();

                if (remaining.Count == 0)
                {
                    var del = new JsonObject { ["filters"] = new JsonArray(new JsonObject { ["name"] = name, ["type"] = "TXT" }) };
                    await Send(HttpMethod.Delete, $"dns/v1/zones/{zone}", del);
                }
                else
                {
                    var put = new JsonObject
                    {
                        ["overwrite"] = true,
                        ["zone"] = new JsonArray(new JsonObject
                        {
                            ["name"] = name, ["type"] = "TXT",
                            ["ttl"] = rrset?["ttl"]?.GetValue<int>() ?? Ttl,
                            ["records"] = new JsonArray(remaining.Select(c => (JsonNode)new JsonObject { ["content"] = c }).ToArray())
                        })
                    };
                    await Send(HttpMethod.Put, $"dns/v1/zones/{zone}", put);
                }
                _added.Remove((zone, name, values));
            }
            catch (Exception ex)
            {
                ActivityLog.Warning("Hostinger", $"Could not remove TXT {name}.{zone}: {ex.Message} — delete it in hPanel.");
            }
        }
    }

    private async Task<JsonNode?> Send(HttpMethod method, string url, JsonNode? body)
    {
        using var req = new HttpRequestMessage(method, url);
        if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        JsonNode? json;
        try { json = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text); } catch { json = null; }
        if (!resp.IsSuccessStatusCode)
        {
            var msg = json?["message"]?.GetValue<string>() ?? $"HTTP {(int)resp.StatusCode}";
            if (json?["errors"] is JsonObject errs)
                msg += " — " + string.Join("; ", errs.Select(e => $"{e.Key}: {string.Join(" ", (e.Value as JsonArray ?? new JsonArray()).Select(v => v?.ToString()))}"));
            if (resp.StatusCode == HttpStatusCode.Unauthorized) msg = "The API token was rejected (Unauthenticated). Create a new token in hPanel → Account → API.";
            throw new HostingerApiException(resp.StatusCode, $"Hostinger API error: {msg}");
        }
        return json;
    }

    public void Dispose() => _http.Dispose();
}

public class HostingerApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}
