using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.Versioning;
using System.ServiceProcess;

namespace CertificateGet.Agent;

public class DeployRequest
{
    public string Slot { get; set; } = "";
    public CertInfo? Certificate { get; set; }
    public string? PfxPassword { get; set; }
    public Dictionary<string, string> Files { get; set; } = new();
}

public class CertInfo
{
    public string Name { get; set; } = "";
    public List<string> Domains { get; set; } = new();
    public string Thumbprint { get; set; } = "";
    public DateTime NotAfter { get; set; }
}

public record StepResult(bool Ok, string Message);

public class DeployResult
{
    public bool Success { get; set; } = true;
    public string? Error { get; set; }
    public List<StepResult> Steps { get; set; } = new();
}

public static class Deployer
{
    public const string DefaultTsplusCertFolder = @"C:\Program Files (x86)\TSplus\UserDesktop\files\cert";
    public const string DefaultTsplusFolder = @"C:\Program Files (x86)\TSplus";
    private static readonly HashSet<string> SecretAliases = new() { "combined", "combined-keyfirst", "key", "encrypted-key", "pfx", "p12", "k8s", "jks" };
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);

    /// <summary>Every file type a slot needs, so the app sends exactly those.</summary>
    public static List<string> RequiredFiles(Slot slot) =>
        slot.Destinations.SelectMany(d =>
                d.Kind.Equals("TSplus", StringComparison.OrdinalIgnoreCase) ? new[] { "pfx" } :
                d.Kind.Equals("TSplusJks", StringComparison.OrdinalIgnoreCase) ? new[] { "jks" } :
                d.Files.Select(f => f.Source))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static async Task<DeployResult> RunAsync(AgentConfig cfg, DeployRequest req)
    {
        var result = new DeployResult();
        void Ok(string m) { result.Steps.Add(new StepResult(true, m)); Log.Write(m); }
        void Fail(string m) { result.Steps.Add(new StepResult(false, m)); result.Success = false; Log.Write("ERROR " + m); }

        var slot = cfg.Slots.FirstOrDefault(s => s.Name.Equals(req.Slot, StringComparison.OrdinalIgnoreCase));
        if (slot == null) return new DeployResult { Success = false, Error = $"Slot \"{req.Slot}\" is not configured on {Environment.MachineName}." };

        await OneAtATime.WaitAsync();
        try
        {
            Log.Write($"Deploy request for slot \"{slot.Name}\": {req.Certificate?.Name} {req.Certificate?.Thumbprint} (expires {req.Certificate?.NotAfter:yyyy-MM-dd})");
            var files = req.Files.ToDictionary(kv => kv.Key, kv => Convert.FromBase64String(kv.Value), StringComparer.OrdinalIgnoreCase);
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var services = new List<string>();
            var programs = new List<ProgramSpec>();
            var commands = new List<(string Cmd, string Folder)>();

            foreach (var dest in slot.Destinations)
            {
                var label = string.IsNullOrWhiteSpace(dest.Name) ? dest.Folder : dest.Name;
                try
                {
                    if (dest.Kind.Equals("TSplus", StringComparison.OrdinalIgnoreCase))
                    {
                        Ok($"{label}: {await ImportTsplusAsync(dest, files, req.PfxPassword)}");
                    }
                    else if (dest.Kind.Equals("TSplusJks", StringComparison.OrdinalIgnoreCase))
                    {
                        Ok($"{label}: {await InstallTsplusJksAsync(cfg, slot, dest, files, stamp)}");
                    }
                    else
                    {
                        Directory.CreateDirectory(dest.Folder);
                        foreach (var spec in dest.Files)
                        {
                            if (!files.TryGetValue(spec.Source, out var bytes))
                                throw new InvalidOperationException($"the app did not send \"{spec.Source}\"");
                            var target = Path.Combine(dest.Folder, spec.FileName);
                            Backup(cfg, slot, dest, target, stamp);
                            WriteAtomic(target, bytes, SecretAliases.Contains(spec.Source));
                        }
                        Ok($"{label}: wrote {string.Join(", ", dest.Files.Select(f => f.FileName))} to {dest.Folder}");
                    }
                    // Blank entries (e.g. "RestartServices": [""]) are ignored.
                    services.AddRange(dest.RestartServices.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
                    programs.AddRange(dest.RestartPrograms.Where(p => !string.IsNullOrWhiteSpace(p.Path)));
                    commands.AddRange(dest.Commands.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => (c, dest.Folder)));
                }
                catch (Exception ex)
                {
                    Fail($"{label}: {ex.Message}");
                }
            }

            foreach (var svc in services.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try { Ok(await RestartServiceAsync(svc)); }
                catch (Exception ex) { Fail($"Restart {svc}: {ex.Message}"); }
            }

            // Each program once, even when several destinations list it.
            foreach (var prog in programs.GroupBy(p => Path.GetFullPath(p.Path), StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
            {
                try
                {
                    if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("RestartPrograms is Windows-only; use Commands on Linux.");
                    Ok(await ProgramRestarter.RestartAsync(prog));
                }
                catch (Exception ex) { Fail($"Restart {Path.GetFileName(prog.Path)}: {ex.Message}"); }
            }

            foreach (var (cmd, folder) in commands.Concat(slot.Commands.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => (c, ""))))
            {
                try { Ok(await RunCommandAsync(cmd.Replace("{folder}", folder))); }
                catch (Exception ex) { Fail(ex.Message); }
            }

            if (!result.Success) result.Error = "One or more steps failed on " + Environment.MachineName + ".";
            Log.Write(result.Success ? "Deploy finished OK." : "Deploy finished with errors.");
            return result;
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private static void WriteAtomic(string target, byte[] bytes, bool secret)
    {
        var tmp = target + ".cg-tmp";
        File.WriteAllBytes(tmp, bytes);
        if (!OperatingSystem.IsWindows())
        {
            if (File.Exists(target))
            {
                // Replacing a file another service reads (e.g. Cockpit's root:cockpit-ws 640 cert):
                // keep its owner, group, mode and SELinux label.
                File.SetUnixFileMode(tmp, File.GetUnixFileMode(target));
                RunQuiet("chown", $"--reference=\"{target}\" \"{tmp}\"");
                RunQuiet("chcon", $"--reference=\"{target}\" \"{tmp}\"");
            }
            else
            {
                File.SetUnixFileMode(tmp, secret
                    ? UnixFileMode.UserRead | UnixFileMode.UserWrite
                    : UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
            }
        }
        File.Move(tmp, target, overwrite: true);
    }

    /// <summary>Runs a small helper (chown/chcon); failures are ignored (e.g. SELinux not present).</summary>
    private static void RunQuiet(string file, string args)
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo(file, args)
                { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true })!;
            p.StandardOutput.ReadToEnd();
            p.StandardError.ReadToEnd();
            p.WaitForExit(10_000);
        }
        catch { /* tool not installed */ }
    }

    /// <summary>Copies the current file into the agent's own backups folder (never next to the live files,
    /// because servers such as HAProxy load every file in their certificate folder).</summary>
    private static void Backup(AgentConfig cfg, Slot slot, Destination dest, string target, string stamp)
    {
        if (!File.Exists(target) || cfg.KeepBackups <= 0) return;
        var destKey = Safe(string.IsNullOrWhiteSpace(dest.Name) ? dest.Folder : dest.Name);
        var root = Path.Combine(AgentConfig.Folder, "backups", Safe(slot.Name), destKey);
        var dir = Path.Combine(root, stamp);
        Directory.CreateDirectory(dir);
        File.Copy(target, Path.Combine(dir, Path.GetFileName(target)), true);
        foreach (var old in Directory.GetDirectories(root).OrderByDescending(d => d).Skip(cfg.KeepBackups))
            try { Directory.Delete(old, true); } catch { }
    }

    private static string Safe(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s.Replace(' ', '_');
    }

    /// <summary>TSplus 15+: CertificateManager.exe /add &lt;pfx&gt;, with the password in a temporary certpassword.txt.</summary>
    private static async Task<string> ImportTsplusAsync(Destination dest, Dictionary<string, byte[]> files, string? password)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("TSplus destinations only work on Windows.");
        if (!files.TryGetValue("pfx", out var pfx)) throw new InvalidOperationException("the app did not send a PFX");
        var certFolder = string.IsNullOrWhiteSpace(dest.TsplusCertFolder) ? DefaultTsplusCertFolder : dest.TsplusCertFolder!;
        var manager = Path.Combine(certFolder, "CertificateManager.exe");
        if (!File.Exists(manager)) throw new FileNotFoundException($"TSplus CertificateManager.exe not found in {certFolder} (TSplus 15 or later is required).");

        // TSplus needs a password-protected PFX; re-wrap it with a random password if it came without one.
        if (string.IsNullOrEmpty(password))
        {
            password = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
            var coll = X509CertificateLoader.LoadPkcs12Collection(pfx, null, X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
            pfx = coll.Export(X509ContentType.Pfx, password)!;
        }

        var work = Path.Combine(AgentConfig.Folder, "work");
        Directory.CreateDirectory(work);
        var pfxPath = Path.Combine(work, $"tsplus-{Guid.NewGuid():N}.pfx");
        var pwdFile = Path.Combine(certFolder, "certpassword.txt");
        try
        {
            await File.WriteAllBytesAsync(pfxPath, pfx);
            await File.WriteAllTextAsync(pwdFile, password);
            var (code, output) = await ExecAsync(manager, $"/add \"{pfxPath}\"", TimeSpan.FromMinutes(2));
            if (code != 0) throw new InvalidOperationException($"CertificateManager exited with {code}: {output}");
            return $"imported into TSplus{(output.Length > 0 ? " (" + output + ")" : "")}";
        }
        finally
        {
            try { File.Delete(pwdFile); } catch { }
            try { File.Delete(pfxPath); } catch { }
        }
    }

    /// <summary>TSplus versions using cert.jks: replace Clients\webserver\cert.jks and run AdminTool.exe /webrestart.</summary>
    private static async Task<string> InstallTsplusJksAsync(AgentConfig cfg, Slot slot, Destination dest, Dictionary<string, byte[]> files, string stamp)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("TSplus destinations only work on Windows.");
        if (!files.TryGetValue("jks", out var jks)) throw new InvalidOperationException("the app did not send cert.jks");
        var root = string.IsNullOrWhiteSpace(dest.TsplusFolder) ? DefaultTsplusFolder : dest.TsplusFolder!;
        var webserver = Path.Combine(root, "Clients", "webserver");
        if (!Directory.Exists(webserver)) throw new DirectoryNotFoundException($"TSplus web server folder not found: {webserver}");
        var target = Path.Combine(webserver, "cert.jks");
        Backup(cfg, slot, dest, target, stamp);
        WriteAtomic(target, jks, secret: true);

        var adminTool = Path.Combine(root, "UserDesktop", "files", "AdminTool.exe");
        if (!File.Exists(adminTool))
            return $"wrote {target}; AdminTool.exe not found at {adminTool} — restart the TSplus web server manually";
        var (code, output) = await ExecAsync(adminTool, "/webrestart", TimeSpan.FromMinutes(3));
        if (code != 0) throw new InvalidOperationException($"wrote {target}, but AdminTool.exe /webrestart exited with {code}: {output}");
        return $"wrote {target} and restarted the TSplus web server";
    }

    private static async Task<string> RestartServiceAsync(string name)
    {
        if (OperatingSystem.IsWindows())
        {
            await Task.Run(() => { if (OperatingSystem.IsWindows()) RestartWindowsService(name); });
            return $"Restarted service {name}";
        }
        var (code, output) = await ExecAsync("systemctl", $"restart {name}", TimeSpan.FromMinutes(2));
        if (code != 0) throw new InvalidOperationException($"systemctl restart {name} failed: {output}");
        return $"Restarted {name}";
    }

    [SupportedOSPlatform("windows")]
    private static void RestartWindowsService(string name)
    {
        using var sc = new ServiceController(name);
        if (sc.Status != ServiceControllerStatus.Stopped)
        {
            sc.Stop();
            sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(90));
        }
        sc.Start();
        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(90));
    }

    private static async Task<string> RunCommandAsync(string command)
    {
        var (code, output) = OperatingSystem.IsWindows()
            ? await ExecAsync("cmd.exe", $"/c {command}", TimeSpan.FromMinutes(2))
            : await ExecAsync("/bin/sh", $"-c \"{command.Replace("\"", "\\\"")}\"", TimeSpan.FromMinutes(2));
        if (code != 0) throw new InvalidOperationException($"Command failed (exit {code}): {command} — {output}");
        return $"Ran: {command}{(output.Length > 0 ? " — " + output : "")}";
    }

    private static async Task<(int Code, string Output)> ExecAsync(string file, string args, TimeSpan timeout)
    {
        var psi = new ProcessStartInfo(file, args)
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {file}");
        var stdout = p.StandardOutput.ReadToEndAsync();
        var stderr = p.StandardError.ReadToEndAsync();
        using var cts = new CancellationTokenSource(timeout);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(true); } catch { }
            throw new System.TimeoutException($"{file} {args} did not finish within {timeout.TotalSeconds:0} s.");
        }
        var output = ((await stdout) + " " + (await stderr)).Trim();
        if (output.Length > 500) output = output[..500] + "…";
        return (p.ExitCode, output);
    }
}

public static class Log
{
    private static readonly object Sync = new();

    public static void Write(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}";
        Console.WriteLine(line);
        lock (Sync)
        {
            try { File.AppendAllText(AgentConfig.LogPath, line + Environment.NewLine); } catch { }
        }
    }
}
