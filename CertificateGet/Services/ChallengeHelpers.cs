using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DnsClient;

namespace CertificateGet.Services;

/// <summary>
/// Tiny HTTP server that answers /.well-known/acme-challenge/{token} requests.
/// Uses a raw TcpListener so no URL ACL / admin rights are needed (port 80 must be free and reachable).
/// </summary>
public sealed class Http01Server : IDisposable
{
    private readonly Dictionary<string, string> _tokens = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public void AddToken(string token, string keyAuthorization) => _tokens[token] = keyAuthorization;

    public void Start(int port)
    {
        _listener = new TcpListener(IPAddress.IPv6Any, port);
        _listener.Server.DualMode = true;
        _listener.Start();
        _cts = new CancellationTokenSource();
        _ = AcceptLoop(_cts.Token);
    }

    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener!.AcceptTcpClientAsync(ct); }
            catch { break; }
            _ = Handle(client);
        }
    }

    private async Task Handle(TcpClient client)
    {
        using (client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                var stream = client.GetStream();
                var buffer = new byte[4096];
                var read = await stream.ReadAsync(buffer);
                var request = Encoding.ASCII.GetString(buffer, 0, read);
                var firstLine = request.Split("\r\n")[0];
                var parts = firstLine.Split(' ');
                var path = parts.Length > 1 ? parts[1] : "";
                const string prefix = "/.well-known/acme-challenge/";

                string status = "404 Not Found", body = "Not found";
                if (path.StartsWith(prefix, StringComparison.Ordinal) &&
                    _tokens.TryGetValue(path[prefix.Length..], out var keyAuth))
                {
                    status = "200 OK";
                    body = keyAuth;
                    ActivityLog.Info("HTTP", $"Served challenge to {client.Client.RemoteEndPoint}");
                }
                var bodyBytes = Encoding.ASCII.GetBytes(body);
                var header = $"HTTP/1.1 {status}\r\nContent-Type: text/plain\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
                await stream.WriteAsync(Encoding.ASCII.GetBytes(header));
                await stream.WriteAsync(bodyBytes);
            }
            catch { /* ignore broken clients */ }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        try { _listener?.Stop(); } catch { }
    }
}

/// <summary>Checks public resolvers for the _acme-challenge TXT values before asking Let's Encrypt to validate.</summary>
public static class DnsChecker
{
    private static LookupClient CreateClient()
    {
        var servers = SettingsService.Current.DnsResolvers
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => IPAddress.TryParse(s, out var ip) ? ip : null)
            .Where(ip => ip != null)
            .Select(ip => new NameServer(ip!))
            .ToList();
        if (servers.Count == 0) servers.Add(new NameServer(IPAddress.Parse("1.1.1.1")));
        return new LookupClient(new LookupClientOptions(servers.ToArray())
        {
            UseCache = false,
            Timeout = TimeSpan.FromSeconds(5),
            Retries = 1
        });
    }

    /// <summary>Returns the TXT values currently visible for <paramref name="recordName"/>.</summary>
    public static async Task<HashSet<string>> GetTxtAsync(string recordName)
    {
        var client = CreateClient();
        var result = new HashSet<string>();
        try
        {
            var response = await client.QueryAsync(recordName, QueryType.TXT);
            foreach (var txt in response.Answers.TxtRecords())
                foreach (var v in txt.Text) result.Add(v);
        }
        catch { /* treat as not found */ }
        return result;
    }

    public static async Task<bool> AllVisibleAsync(IEnumerable<Models.DnsTxtRecord> records)
    {
        var all = true;
        foreach (var group in records.GroupBy(r => r.RecordName, StringComparer.OrdinalIgnoreCase))
        {
            var values = await GetTxtAsync(group.Key);
            foreach (var r in group)
            {
                r.Found = values.Contains(r.Value);
                all &= r.Found;
            }
        }
        return all;
    }
}

/// <summary>Minimal Cloudflare DNS API client for creating/removing _acme-challenge TXT records.</summary>
public sealed class CloudflareDns : IDisposable
{
    private readonly HttpClient _http;
    private readonly List<(string ZoneId, string RecordId)> _created = new();

    public CloudflareDns(string apiToken)
    {
        _http = new HttpClient { BaseAddress = new Uri("https://api.cloudflare.com/client/v4/") };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
    }

    public async Task<string> VerifyTokenAsync()
    {
        var json = await Send(HttpMethod.Get, "user/tokens/verify", null);
        return json["result"]?["status"]?.GetValue<string>() ?? "unknown";
    }

    private async Task<string> FindZoneIdAsync(string recordName)
    {
        // Walk up the labels: _acme-challenge.www.example.co.uk → www.example.co.uk → example.co.uk ...
        var labels = recordName.TrimEnd('.').Split('.');
        for (var i = 1; i < labels.Length - 1; i++)
        {
            var candidate = string.Join('.', labels.Skip(i));
            var json = await Send(HttpMethod.Get, $"zones?name={Uri.EscapeDataString(candidate)}", null);
            var arr = json["result"] as JsonArray;
            if (arr is { Count: > 0 }) return arr[0]!["id"]!.GetValue<string>();
        }
        throw new InvalidOperationException($"No Cloudflare zone found for {recordName}. Check that the token has Zone:Read and DNS:Edit permission for this domain.");
    }

    public async Task CreateTxtAsync(string recordName, string value)
    {
        var zoneId = await FindZoneIdAsync(recordName);
        var body = new JsonObject { ["type"] = "TXT", ["name"] = recordName, ["content"] = value, ["ttl"] = 60, ["comment"] = "CertificateGet ACME challenge" };
        var json = await Send(HttpMethod.Post, $"zones/{zoneId}/dns_records", body);
        var id = json["result"]?["id"]?.GetValue<string>() ?? throw new InvalidOperationException("Cloudflare did not return a record id.");
        _created.Add((zoneId, id));
    }

    public async Task CleanupAsync()
    {
        foreach (var (zone, rec) in _created.ToList())
        {
            try
            {
                await Send(HttpMethod.Delete, $"zones/{zone}/dns_records/{rec}", null);
                _created.Remove((zone, rec));
            }
            catch (Exception ex)
            {
                ActivityLog.Warning("Cloudflare", $"Could not remove TXT record {rec}: {ex.Message}");
            }
        }
    }

    private async Task<JsonNode> Send(HttpMethod method, string url, JsonNode? body)
    {
        using var req = new HttpRequestMessage(method, url);
        if (body != null) req.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        using var resp = await _http.SendAsync(req);
        var text = await resp.Content.ReadAsStringAsync();
        JsonNode? json;
        try { json = JsonNode.Parse(text); } catch { json = null; }
        if (!resp.IsSuccessStatusCode || json?["success"]?.GetValue<bool>() != true)
        {
            var err = json?["errors"] is JsonArray errs && errs.Count > 0
                ? string.Join("; ", errs.Select(e => e?["message"]?.GetValue<string>()))
                : $"HTTP {(int)resp.StatusCode}";
            throw new InvalidOperationException($"Cloudflare API error: {err}");
        }
        return json!;
    }

    public void Dispose() => _http.Dispose();
}
