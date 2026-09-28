using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using CertificateGet.Models;

namespace CertificateGet.Services;

/// <summary>
/// DNS Made Easy REST API v2.0 client (https://api.dnsmadeeasy.com/V2.0) for the _acme-challenge TXT records.
/// Every TXT value is its own record, so a wildcard and its bare domain simply get two records under one name,
/// and cleanup deletes only the record ids this run created.
/// Requests are signed: x-dnsme-hmac = hex(HMAC-SHA1(secret key, x-dnsme-requestDate)).
/// </summary>
public sealed class DnsMadeEasyDns : IDnsProvider
{
    private const int Ttl = 120;
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly byte[] _secret;
    private readonly List<(long DomainId, string Zone, string Name, long RecordId)> _created = new();

    public string DisplayName => "DNS Made Easy";

    /// <param name="handler">Optional HTTP handler (used by tests to simulate the API).</param>
    public DnsMadeEasyDns(string apiKey, string secretKey, HttpMessageHandler? handler = null)
    {
        _apiKey = apiKey.Trim();
        _secret = Encoding.UTF8.GetBytes(secretKey.Trim());
        _http = new HttpClient(handler ?? new HttpClientHandler()) { BaseAddress = new Uri("https://api.dnsmadeeasy.com/V2.0/") };
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>Domain names in the account (used by the "Test keys" button).</summary>
    public async Task<List<string>> ListDomainsAsync()
    {
        // Paging is documented loosely, so stop when a page brings nothing new.
        var names = new HashSet<string>();
        for (var page = 0; page < 50; page++)
        {
            var json = await Send(HttpMethod.Get, $"dns/managed/?page={page}&rows=100", null);
            var added = 0;
            foreach (var d in json?["data"] as JsonArray ?? new JsonArray())
                if (d?["name"]?.ToString() is { Length: > 0 } n && names.Add(n.ToLowerInvariant())) added++;
            var totalPages = json?["totalPages"] is { } tp && int.TryParse(tp.ToString(), out var t) ? t : 1;
            if (added == 0 || page + 1 >= totalPages) break;
        }
        return names.ToList();
    }

    private async Task<(long Id, string Zone)> FindDomainAsync(string recordName)
    {
        var labels = recordName.TrimEnd('.').ToLowerInvariant().Split('.');
        for (var i = 1; i < labels.Length - 1; i++)
        {
            var candidate = string.Join('.', labels.Skip(i));
            try
            {
                var json = await Send(HttpMethod.Get, $"dns/managed/name?domainname={Uri.EscapeDataString(candidate)}", null);
                if (json?["id"] is { } id) return (long.Parse(id.ToString(), CultureInfo.InvariantCulture), candidate);
            }
            catch (DnsMadeEasyApiException ex) when (ex.Status is HttpStatusCode.NotFound or HttpStatusCode.BadRequest)
            {
                // not a managed domain; try the parent
            }
        }
        throw new InvalidOperationException($"No DNS Made Easy domain found for {recordName}. Check that the domain is managed in this account.");
    }

    public async Task AddTxtRecordsAsync(IList<DnsTxtRecord> records)
    {
        foreach (var group in records.GroupBy(r => r.RecordName.ToLowerInvariant()))
        {
            var (domainId, zone) = await FindDomainAsync(group.Key);
            var name = group.Key[..^(zone.Length + 1)];
            foreach (var value in group.Select(r => r.Value).Distinct())
            {
                var body = new JsonObject
                {
                    ["name"] = name, ["type"] = "TXT", ["value"] = $"\"{value}\"", ["ttl"] = Ttl, ["gtdLocation"] = "DEFAULT"
                };
                var created = await Send(HttpMethod.Post, $"dns/managed/{domainId}/records/", body);
                var id = created?["id"] is { } n ? long.Parse(n.ToString(), CultureInfo.InvariantCulture)
                    : throw new InvalidOperationException("DNS Made Easy did not return a record id.");
                _created.Add((domainId, zone, name, id));
            }
            ActivityLog.Info("DNS Made Easy", $"Added TXT {name}.{zone} ({group.Count()} value(s)).");
        }
    }

    public async Task CleanupAsync()
    {
        foreach (var rec in _created.ToList())
        {
            try
            {
                await Send(HttpMethod.Delete, $"dns/managed/{rec.DomainId}/records/{rec.RecordId}", null);
                _created.Remove(rec);
            }
            catch (DnsMadeEasyApiException ex) when (ex.Status == HttpStatusCode.NotFound)
            {
                _created.Remove(rec); // already gone
            }
            catch (Exception ex)
            {
                ActivityLog.Warning("DNS Made Easy", $"Could not remove TXT {rec.Name}.{rec.Zone}: {ex.Message} — delete it in the DNS Made Easy control panel.");
            }
        }
    }

    private async Task<JsonNode?> Send(HttpMethod method, string url, JsonNode? body)
    {
        using var req = new HttpRequestMessage(method, url);
        var date = DateTime.UtcNow.ToString("r", CultureInfo.InvariantCulture);
        req.Headers.Add("x-dnsme-apiKey", _apiKey);
        req.Headers.Add("x-dnsme-requestDate", date);
        req.Headers.Add("x-dnsme-hmac", Convert.ToHexString(HMACSHA1.HashData(_secret, Encoding.UTF8.GetBytes(date))).ToLowerInvariant());
        if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        JsonNode? json;
        try { json = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text); } catch { json = null; }
        if (!resp.IsSuccessStatusCode)
        {
            var msg = json?["error"] is JsonArray errs && errs.Count > 0
                ? string.Join("; ", errs.Select(e => e?.ToString()))
                : json?["error"]?.ToString() is { Length: > 0 } e1 ? e1 : $"HTTP {(int)resp.StatusCode}";
            if (resp.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                msg = $"The API key / secret key were rejected ({msg}). Check both keys in DNS Made Easy → Config → Account Information, " +
                      "and that this PC's clock is correct.";
            throw new DnsMadeEasyApiException(resp.StatusCode, $"DNS Made Easy API error: {msg}");
        }
        return json;
    }

    public void Dispose() => _http.Dispose();
}

public class DnsMadeEasyApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}
