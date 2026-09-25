using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CertificateGet.Models;

namespace CertificateGet.Services;

/// <summary>
/// Constellix DNS API v4 client (https://api.dns.constellix.com/v4) for the _acme-challenge TXT records.
/// Constellix keeps all TXT values of a name in one record, so values are merged into an existing record
/// (a wildcard and its bare domain share one name) and cleanup removes only the values this run added.
/// </summary>
public sealed class ConstellixDns : IDnsProvider
{
    private const int Ttl = 60;
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly byte[] _secret;
    private readonly List<(long DomainId, string Zone, string Name, long RecordId, bool Created, HashSet<string> Values)> _changes = new();

    public string DisplayName => "Constellix";

    /// <param name="handler">Optional HTTP handler (used by tests to simulate the API).</param>
    public ConstellixDns(string apiKey, string secretKey, HttpMessageHandler? handler = null)
    {
        _apiKey = apiKey.Trim();
        _secret = Encoding.UTF8.GetBytes(secretKey.Trim());
        _http = new HttpClient(handler ?? new HttpClientHandler()) { BaseAddress = new Uri("https://api.dns.constellix.com/v4/") };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Bearer token: apiKey:base64(HMAC-SHA1(timestampMs, secret)):timestampMs — built per request.</summary>
    private string Token()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        var hmac = Convert.ToBase64String(HMACSHA1.HashData(_secret, Encoding.UTF8.GetBytes(ts)));
        return $"{_apiKey}:{hmac}:{ts}";
    }

    /// <summary>Domain names in the account (used by the "Test keys" button).</summary>
    public async Task<List<string>> ListDomainsAsync()
    {
        var names = new List<string>();
        for (var page = 1; page <= 50; page++)
        {
            var json = await Send(HttpMethod.Get, $"domains?page={page}&perPage=100", null);
            foreach (var d in json?["data"] as JsonArray ?? new JsonArray())
                if (d?["name"]?.ToString() is { Length: > 0 } n) names.Add(n.ToLowerInvariant());
            if (page >= (json?["meta"]?["pagination"]?["totalPages"] is { } tp ? int.Parse(tp.ToString()) : 1)) break;
        }
        return names;
    }

    private async Task<(long Id, string Zone)> FindDomainAsync(string recordName)
    {
        var labels = recordName.TrimEnd('.').ToLowerInvariant().Split('.');
        for (var i = 1; i < labels.Length - 1; i++)
        {
            var candidate = string.Join('.', labels.Skip(i));
            var json = await Send(HttpMethod.Get, $"search/domains?name={Uri.EscapeDataString(candidate)}", null);
            var match = (json?["data"] as JsonArray ?? new JsonArray())
                .FirstOrDefault(d => string.Equals(d?["name"]?.ToString(), candidate, StringComparison.OrdinalIgnoreCase));
            if (match != null) return (Id(match["id"]), candidate);
        }
        throw new InvalidOperationException($"No Constellix domain found for {recordName}. Check that the domain is in this Constellix account.");
    }

    private async Task<JsonNode?> FindTxtRecordAsync(long domainId, string name)
    {
        for (var page = 1; page <= 100; page++)
        {
            var json = await Send(HttpMethod.Get, $"domains/{domainId}/records?page={page}&perPage=100", null);
            var hit = (json?["data"] as JsonArray ?? new JsonArray()).FirstOrDefault(r =>
                r?["type"]?.ToString() == "TXT" &&
                string.Equals(r?["name"]?.ToString(), name, StringComparison.OrdinalIgnoreCase));
            if (hit != null) return hit;
            if (page >= (json?["meta"]?["pagination"]?["totalPages"] is { } tp ? int.Parse(tp.ToString()) : 1)) break;
        }
        return null;
    }

    private static List<string> ValuesOf(JsonNode? record) =>
        (record?["value"] as JsonArray ?? new JsonArray())
            .Select(v => (v is JsonObject o ? o["value"]?.ToString() : v?.ToString()) ?? "")
            .Where(v => v.Length > 0)
            .ToList();

    private static JsonArray ValueArray(IEnumerable<string> values) =>
        new(values.Select(v => (JsonNode)new JsonObject { ["value"] = v, ["enabled"] = true }).ToArray());

    private static long? IdOrNull(JsonNode? n) => n == null ? null : long.Parse(n.ToString());
    private static long Id(JsonNode? n) => IdOrNull(n) ?? throw new InvalidOperationException("Constellix returned a record without an id.");

    private static string Unquote(string v) => v.Trim().Trim('"');

    public async Task AddTxtRecordsAsync(IList<DnsTxtRecord> records)
    {
        foreach (var group in records.GroupBy(r => r.RecordName.ToLowerInvariant()))
        {
            var (domainId, zone) = await FindDomainAsync(group.Key);
            var name = group.Key[..^(zone.Length + 1)];
            var ours = group.Select(r => r.Value).ToHashSet();
            var existing = await FindTxtRecordAsync(domainId, name);
            if (existing == null)
            {
                var body = new JsonObject
                {
                    ["name"] = name, ["type"] = "TXT", ["ttl"] = Ttl, ["mode"] = "standard",
                    ["notes"] = "CertificateGet ACME challenge", ["value"] = ValueArray(ours)
                };
                var created = await Send(HttpMethod.Post, $"domains/{domainId}/records", body);
                var id = IdOrNull(created?["data"]?["id"]) ?? throw new InvalidOperationException("Constellix did not return a record id.");
                _changes.Add((domainId, zone, name, id, true, ours));
            }
            else
            {
                var id = Id(existing["id"]);
                var merged = ValuesOf(existing).Where(v => !ours.Contains(Unquote(v))).Concat(ours).ToList();
                await Send(HttpMethod.Put, $"domains/{domainId}/records/{id}", new JsonObject { ["ttl"] = Ttl, ["value"] = ValueArray(merged) });
                _changes.Add((domainId, zone, name, id, false, ours));
            }
            ActivityLog.Info("Constellix", $"Added TXT {name}.{zone} ({ours.Count} value(s)).");
        }
    }

    public async Task CleanupAsync()
    {
        foreach (var change in _changes.ToList())
        {
            var (domainId, zone, name, id, _, ours) = change;
            try
            {
                var current = await Send(HttpMethod.Get, $"domains/{domainId}/records/{id}", null);
                var remaining = ValuesOf(current?["data"]).Where(v => !ours.Contains(Unquote(v))).ToList();
                if (remaining.Count == 0)
                    await Send(HttpMethod.Delete, $"domains/{domainId}/records/{id}", null);
                else
                    await Send(HttpMethod.Put, $"domains/{domainId}/records/{id}", new JsonObject { ["value"] = ValueArray(remaining) });
                _changes.Remove(change);
            }
            catch (ConstellixApiException ex) when (ex.Status == HttpStatusCode.NotFound)
            {
                _changes.Remove(change); // already gone
            }
            catch (Exception ex)
            {
                ActivityLog.Warning("Constellix", $"Could not remove TXT {name}.{zone}: {ex.Message} — delete it in the Constellix portal.");
            }
        }
    }

    private async Task<JsonNode?> Send(HttpMethod method, string url, JsonNode? body)
    {
        using var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        JsonNode? json;
        try { json = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text); } catch { json = null; }
        if (!resp.IsSuccessStatusCode)
        {
            var parts = new List<string>();
            if (json?["message"]?.ToString() is { Length: > 0 } m) parts.Add(m);
            if (json?["errors"] is JsonArray errs) parts.AddRange(errs.Select(e => e?.ToString() ?? ""));
            else if (json?["errors"] is JsonObject eo) parts.AddRange(eo.Select(kv => $"{kv.Key}: {kv.Value}"));
            var msg = parts.Count > 0 ? string.Join("; ", parts) : $"HTTP {(int)resp.StatusCode}";
            const string keyHelp = "Check both keys in Constellix → Edit My Account → API Keys, and that this PC's clock is correct.";
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                msg = $"The API key / secret key were rejected ({msg}). {keyHelp}";
            // Constellix answers an unknown API key with a bare 500 "Server Error".
            else if (resp.StatusCode == HttpStatusCode.InternalServerError && msg == "Server Error")
                msg = $"Constellix returned \"Server Error\", which is what it answers for an unknown API key. {keyHelp}";
            throw new ConstellixApiException(resp.StatusCode, $"Constellix API error: {msg}");
        }
        return json;
    }

    public void Dispose() => _http.Dispose();
}

public class ConstellixApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}
