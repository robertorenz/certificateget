using System.Text.Json;

namespace CertificateGet.Services;

/// <summary>
/// ZeroSSL's ACME server only accepts new accounts that carry External Account Binding (EAB) credentials.
/// They come from ZeroSSL's API: with the account's API access key, or (no ZeroSSL login needed) with an e-mail
/// address, which creates or reuses the ZeroSSL account for that address. Only used once, when the ACME
/// account is created; the ACME account key is what is kept afterwards.
/// </summary>
public static class ZeroSsl
{
    public static readonly Uri Directory = new("https://acme.zerossl.com/v2/DV90");

    public record Eab(string KeyId, string HmacKey);

    public static async Task<Eab> GetEabAsync(string? apiKey, string? email, CancellationToken ct = default)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        HttpResponseMessage res;
        if (!string.IsNullOrWhiteSpace(apiKey))
            res = await http.PostAsync("https://api.zerossl.com/acme/eab-credentials?access_key=" + Uri.EscapeDataString(apiKey.Trim()), null, ct);
        else if (!string.IsNullOrWhiteSpace(email))
            res = await http.PostAsync("https://api.zerossl.com/acme/eab-credentials-email",
                new FormUrlEncodedContent(new Dictionary<string, string> { ["email"] = email.Trim() }), ct);
        else
            throw new InvalidOperationException(
                "ZeroSSL needs a contact e-mail or a ZeroSSL API key to create the ACME account. Enter an e-mail on the certificate, or set it in Settings.");

        var body = await res.Content.ReadAsStringAsync(ct);
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.True &&
                root.TryGetProperty("eab_kid", out var kid) && root.TryGetProperty("eab_hmac_key", out var hmac))
                return new Eab(kid.GetString()!, hmac.GetString()!);
            if (root.TryGetProperty("error", out var err))
            {
                var info = err.TryGetProperty("info", out var i) ? i.GetString()
                         : err.TryGetProperty("type", out var t) ? t.GetString() : null;
                throw new InvalidOperationException("ZeroSSL refused the EAB request: " + (info ?? err.ToString()) +
                    (string.IsNullOrWhiteSpace(apiKey) ? "" : " Check the ZeroSSL API key in Settings."));
            }
        }
        catch (JsonException) { }
        throw new InvalidOperationException($"ZeroSSL returned an unexpected answer ({(int)res.StatusCode}): {Trim(body)}");
    }

    private static string Trim(string s) => s.Length > 200 ? s[..200] + "…" : s;
}
