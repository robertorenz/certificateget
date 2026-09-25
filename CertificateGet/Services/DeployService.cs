using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.Json.Nodes;
using CertificateGet.Models;
using Renci.SshNet;

namespace CertificateGet.Services;

/// <summary>Asks the user to trust a server the first time the app connects to it.</summary>
public interface IDeployUi
{
    bool ConfirmFingerprint(string targetName, string what, string fingerprint);
}

/// <summary>Pushes an issued certificate to SFTP servers and CertificateGet agents.</summary>
public static class DeployService
{
    /// <summary>Format aliases used in file mappings and in the agent protocol.</summary>
    public static readonly (string Alias, string FormatId, string Label)[] Sources =
    {
        ("combined", CertFileKind.Combined, "Combined PEM (full chain + key)"),
        ("combined-keyfirst", CertFileKind.CombinedKeyFirst, "Combined PEM (key first)"),
        ("fullchain", CertFileKind.FullChain, "Full chain PEM"),
        ("fullchain-root", CertFileKind.FullChainRoot, "Full chain + root PEM"),
        ("cer", CertFileKind.Cer, "Certificate (PEM)"),
        ("crt", CertFileKind.Crt, "Certificate (PEM, .crt)"),
        ("der", CertFileKind.DerCer, "Certificate (DER)"),
        ("chain", CertFileKind.Chain, "Chain (PEM)"),
        ("key", CertFileKind.Key, "Private key (PEM)"),
        ("encrypted-key", CertFileKind.EncryptedKey, "Private key (encrypted)"),
        ("pfx", CertFileKind.Pfx, "PFX"),
        ("p12", CertFileKind.P12, "P12"),
        ("p7b", CertFileKind.P7b, "P7B"),
        ("k8s", CertFileKind.Kubernetes, "Kubernetes secret"),
        ("jks", CertFileKind.Jks, "JKS (Java keystore)"),
    };

    private static readonly HashSet<string> SecretAliases = new() { "combined", "combined-keyfirst", "key", "encrypted-key", "pfx", "p12", "k8s", "jks" };

    public static string FormatIdOf(string alias) =>
        Sources.FirstOrDefault(s => s.Alias == alias).FormatId ?? throw new InvalidOperationException($"Unknown file type \"{alias}\".");

    public static string Expand(string template, CertificateProfile p) =>
        template.Replace("{domain}", CertificateStore.BaseFileName(p)).Replace("{name}", CertificateStore.SafeName(p.Name));

    /// <summary>Deploys the latest issuance to every enabled target (or only the automatic ones).</summary>
    public static async Task<(int Ok, int Failed)> DeployAllAsync(CertificateProfile p, bool automaticOnly,
        Action<LogLevel, string> report, IDeployUi ui)
    {
        int ok = 0, failed = 0;
        if (p.Latest == null) return (0, 0);
        foreach (var t in p.Targets.Where(t => t.Enabled && (!automaticOnly || t.AutoDeploy)).ToList())
        {
            if (await DeployAsync(p, p.Latest, t, report, ui)) ok++; else failed++;
        }
        return (ok, failed);
    }

    public static async Task<bool> DeployAsync(CertificateProfile p, IssuedCertificate issued, DeployTarget t,
        Action<LogLevel, string> report, IDeployUi ui)
    {
        void Step(LogLevel level, string msg)
        {
            ActivityLog.Write(level, "Deploy", $"[{t.Name}] {msg}", p.Name);
            report(level, $"[{t.Name}] {msg}");
        }

        var temp = Path.Combine(Path.GetTempPath(), "cg-deploy-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Step(LogLevel.Info, $"Deploying certificate (expires {issued.NotAfter.ToLocalTime():yyyy-MM-dd}) via {t.TypeDisplay}…");
            var pfxPassword = Secret.Unprotect(p.ProtectedPfxPassword);
            if (t.Type == DeployType.Sftp)
                await Task.Run(() => DeploySftp(p, issued, t, temp, pfxPassword, Step, ui));
            else
                await DeployAgent(p, issued, t, temp, pfxPassword, Step, ui);

            t.LastSuccess = true;
            t.LastMessage = "OK";
            Step(LogLevel.Success, "Deployment finished.");
            return true;
        }
        catch (Exception ex)
        {
            t.LastSuccess = false;
            t.LastMessage = ex.Message;
            Step(LogLevel.Error, "Deployment failed: " + ex.Message);
            return false;
        }
        finally
        {
            t.LastDeployUtc = DateTime.UtcNow;
            try { CertificateStore.Save(p); } catch { /* status only */ }
            try { if (Directory.Exists(temp)) Directory.Delete(temp, true); } catch { }
        }
    }

    /// <summary>Generates the requested aliases into a temp folder and returns alias → local path.</summary>
    private static Dictionary<string, string> Materialize(CertificateProfile p, IssuedCertificate issued, string temp,
        IEnumerable<string> aliases, string? pfxPassword)
    {
        var list = aliases.Distinct().ToList();
        var (_, notes) = CertificateStore.Export(p, issued, temp, list.Select(FormatIdOf), pfxPassword);
        if (notes.Count > 0) throw new InvalidOperationException(string.Join(" ", notes));
        var b = CertificateStore.BaseFileName(p);
        return list.ToDictionary(a => a, a => Path.Combine(temp, CertFileKind.Get(FormatIdOf(a)).FileNames(b)[0]));
    }

    // ---------------- SFTP ----------------

    private static ConnectionInfo Connection(DeployTarget t)
    {
        AuthenticationMethod auth;
        if (t.UseKeyAuth)
        {
            if (string.IsNullOrWhiteSpace(t.PrivateKeyPath) || !File.Exists(t.PrivateKeyPath))
                throw new InvalidOperationException($"SSH private key file not found: {t.PrivateKeyPath}");
            var pass = Secret.Unprotect(t.ProtectedKeyPassphrase);
            var key = string.IsNullOrEmpty(pass) ? new PrivateKeyFile(t.PrivateKeyPath) : new PrivateKeyFile(t.PrivateKeyPath, pass);
            auth = new PrivateKeyAuthenticationMethod(t.Username, key);
        }
        else
        {
            auth = new PasswordAuthenticationMethod(t.Username, Secret.Unprotect(t.ProtectedPassword) ?? "");
        }
        return new ConnectionInfo(t.Host, t.Port, t.Username, auth) { Timeout = TimeSpan.FromSeconds(20) };
    }

    private static void PinHostKey(BaseClient client, DeployTarget t, IDeployUi ui)
    {
        client.HostKeyReceived += (_, e) =>
        {
            var fp = "SHA256:" + e.FingerPrintSHA256;
            if (string.IsNullOrEmpty(t.HostKeyFingerprint))
            {
                e.CanTrust = ui.ConfirmFingerprint(t.Name, $"SSH host key of {t.Host}", fp);
                if (e.CanTrust) t.HostKeyFingerprint = fp;
            }
            else
            {
                e.CanTrust = t.HostKeyFingerprint == fp;
            }
        };
    }

    private static void DeploySftp(CertificateProfile p, IssuedCertificate issued, DeployTarget t, string temp, string? pfxPassword,
        Action<LogLevel, string> step, IDeployUi ui)
    {
        if (t.Files.Count == 0) throw new InvalidOperationException("No files configured for this target.");
        var local = Materialize(p, issued, temp, t.Files.Select(f => f.Source), pfxPassword);
        var conn = Connection(t);
        var folder = t.RemoteFolder.TrimEnd('/');

        using (var sftp = new SftpClient(conn))
        {
            PinHostKey(sftp, t, ui);
            try { sftp.Connect(); }
            catch (Renci.SshNet.Common.SshConnectionException ex) when (ex.Message.Contains("Key exchange negotiation failed") || ex.Message.Contains("host key"))
            {
                throw new InvalidOperationException("The server's SSH host key does not match the one saved for this target. " +
                                                    "If the server was reinstalled, clear the saved fingerprint in the target settings.");
            }
            step(LogLevel.Info, $"Connected to {t.Host}.");
            if (!sftp.Exists(folder)) throw new InvalidOperationException($"Remote folder does not exist: {folder}");

            foreach (var f in t.Files)
            {
                var remote = folder + "/" + Expand(f.RemoteName, p);
                var tmp = remote + ".cg-upload";
                using (var fs = File.OpenRead(local[f.Source]))
                    sftp.UploadFile(fs, tmp, true);
                sftp.ChangePermissions(tmp, SecretAliases.Contains(f.Source) ? (short)0x180 /*600*/ : (short)0x1A4 /*644*/);
                try { sftp.RenameFile(tmp, remote, true); } // atomic replace (posix-rename)
                catch
                {
                    if (sftp.Exists(remote)) sftp.DeleteFile(remote);
                    sftp.RenameFile(tmp, remote);
                }
                step(LogLevel.Info, $"Uploaded {remote}");
            }
        }

        if (!string.IsNullOrWhiteSpace(t.PostCommand))
        {
            using var ssh = new SshClient(conn);
            PinHostKey(ssh, t, ui);
            ssh.Connect();
            var cmd = ssh.CreateCommand(t.PostCommand);
            cmd.CommandTimeout = TimeSpan.FromMinutes(2);
            cmd.Execute();
            var output = (cmd.Result + " " + cmd.Error).Trim();
            if (cmd.ExitStatus != 0)
                throw new InvalidOperationException($"Command failed (exit {cmd.ExitStatus}): {output}");
            step(LogLevel.Info, $"Ran \"{t.PostCommand}\"{(output.Length > 0 ? ": " + output : "")}");
        }
    }

    // ---------------- Agent ----------------

    private static HttpClient AgentClient(DeployTarget t, IDeployUi ui)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, cert, _, _) =>
            {
                if (cert == null) return false;
                var fp = Convert.ToHexString(SHA256.HashData(cert.RawData));
                if (string.IsNullOrEmpty(t.AgentFingerprint))
                {
                    if (!ui.ConfirmFingerprint(t.Name, "TLS certificate of the agent", fp)) return false;
                    t.AgentFingerprint = fp;
                    return true;
                }
                return string.Equals(t.AgentFingerprint, fp, StringComparison.OrdinalIgnoreCase);
            }
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri(t.AgentUrl.TrimEnd('/') + "/"), Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.Add("X-Api-Key", Secret.Unprotect(t.ProtectedApiKey) ?? "");
        return http;
    }

    private static async Task<JsonNode> AgentCall(HttpClient http, HttpMethod method, string path, object? body = null)
    {
        using var req = new HttpRequestMessage(method, path);
        if (body != null) req.Content = JsonContent.Create(body);
        HttpResponseMessage resp;
        try { resp = await http.SendAsync(req); }
        catch (HttpRequestException ex) when (ex.InnerException is System.Security.Authentication.AuthenticationException)
        {
            throw new InvalidOperationException("The agent's TLS certificate does not match the one saved for this target (or was not trusted). " +
                                                "If the agent was reinstalled, clear the saved fingerprint in the target settings.");
        }
        using (resp)
        {
            var text = await resp.Content.ReadAsStringAsync();
            JsonNode? json = null;
            try { json = string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text); } catch { }
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException(json?["error"]?.ToString() ?? $"Agent returned HTTP {(int)resp.StatusCode}.");
            return json ?? new JsonObject();
        }
    }

    /// <summary>Returns the slot names configured on an agent (used by the target editor's "Test" button).</summary>
    public static async Task<List<(string Name, string Description, int Destinations)>> GetAgentSlotsAsync(DeployTarget t, IDeployUi ui)
    {
        using var http = AgentClient(t, ui);
        var json = await AgentCall(http, HttpMethod.Get, "api/slots");
        return (json["slots"] as JsonArray ?? new JsonArray())
            .Select(s => (s?["name"]?.ToString() ?? "", s?["description"]?.ToString() ?? "", s?["destinations"]?.GetValue<int>() ?? 0))
            .ToList();
    }

    private static async Task DeployAgent(CertificateProfile p, IssuedCertificate issued, DeployTarget t, string temp, string? pfxPassword,
        Action<LogLevel, string> step, IDeployUi ui)
    {
        using var http = AgentClient(t, ui);
        var slot = await AgentCall(http, HttpMethod.Get, $"api/slots/{Uri.EscapeDataString(t.AgentSlot)}");
        var aliases = (slot["files"] as JsonArray ?? new JsonArray()).Select(x => x!.ToString()).ToList();
        if (aliases.Count == 0) throw new InvalidOperationException($"Slot \"{t.AgentSlot}\" on the agent asks for no files.");
        var local = await Task.Run(() => Materialize(p, issued, temp, aliases, pfxPassword));

        var payload = new
        {
            slot = t.AgentSlot,
            certificate = new { name = p.Name, domains = p.Domains, thumbprint = issued.Thumbprint, notAfter = issued.NotAfter },
            pfxPassword = aliases.Any(a => a is "pfx" or "p12" or "encrypted-key") ? pfxPassword : null,
            files = local.ToDictionary(kv => kv.Key, kv => Convert.ToBase64String(File.ReadAllBytes(kv.Value)))
        };
        var result = await AgentCall(http, HttpMethod.Post, "api/deploy", payload);
        foreach (var s in result["steps"] as JsonArray ?? new JsonArray())
        {
            var ok = s?["ok"]?.GetValue<bool>() ?? true;
            step(ok ? LogLevel.Info : LogLevel.Error, s?["message"]?.ToString() ?? "");
        }
        if (result["success"]?.GetValue<bool>() != true)
            throw new InvalidOperationException(result["error"]?.ToString() ?? "The agent reported a failure.");
    }

    /// <summary>Tests an SFTP target: connects, checks the folder, and runs nothing.</summary>
    public static Task<string> TestSftpAsync(DeployTarget t, IDeployUi ui) => Task.Run(() =>
    {
        using var sftp = new SftpClient(Connection(t));
        PinHostKey(sftp, t, ui);
        sftp.Connect();
        var folder = t.RemoteFolder.TrimEnd('/');
        if (!sftp.Exists(folder)) return $"Connected to {t.Host}, but the folder {folder} does not exist.";
        return $"Connected to {t.Host} as {t.Username}. Folder {folder} exists.";
    });
}
