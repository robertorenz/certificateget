using System.Diagnostics;
using System.Net;
using CertificateGet.Agent;

// ---------- command line ----------
var command = args.FirstOrDefault()?.ToLowerInvariant() ?? "run";
switch (command)
{
    case "install": return Cli.Install(args);
    case "uninstall": return Cli.Uninstall();
    case "newkey": return Cli.NewKey();
    case "info": return Cli.Info();
    case "check": return Cli.Check();
    case "run": break;
    default:
        Console.WriteLine("""
            CertificateGet Agent — receives renewed certificates from the CertificateGet app.

              install [port]   create agent.json, API key and TLS certificate, register and start the service
              uninstall        stop and remove the service (keeps agent.json)
              newkey           generate a new API key (the old one stops working)
              info             show URL, TLS fingerprint and configured slots
              check            validate agent.json (folders, services, file types)
              run              run in this console (default)
            """);
        return 1;
}

// ---------- web host ----------
var cfg = AgentConfig.Load();
var tls = cfg.LoadOrCreateTlsCertificate();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AgentConfig.Folder });
builder.Host.UseWindowsService(o => o.ServiceName = Cli.ServiceName);
builder.Host.UseSystemd();
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(cfg.Port, l => l.UseHttps(tls));
    k.Limits.MaxRequestBodySize = 20 * 1024 * 1024;
});

var app = builder.Build();

app.Use(async (ctx, next) =>
{
    var current = AgentConfig.Load(); // re-read so edits to agent.json apply without a restart
    var ip = ClientIp(ctx);
    if (current.AllowedIps.Count > 0 && !current.AllowedIps.Contains(ip) && !(ip is "127.0.0.1" or "::1"))
    {
        Log.Write($"Rejected connection from {ip} (not in AllowedIps).");
        ctx.Response.StatusCode = 403;
        await ctx.Response.WriteAsJsonAsync(new { error = $"{ip} is not allowed to use this agent." });
        return;
    }
    if (!current.CheckKey(ctx.Request.Headers["X-Api-Key"]))
    {
        Log.Write($"Rejected request from {ip}: invalid API key.");
        await Task.Delay(1500);
        ctx.Response.StatusCode = 401;
        await ctx.Response.WriteAsJsonAsync(new { error = "Invalid API key." });
        return;
    }
    ctx.Items["cfg"] = current;
    await next();
});

app.MapGet("/api/slots", (HttpContext ctx) =>
{
    var c = (AgentConfig)ctx.Items["cfg"]!;
    return Results.Json(new
    {
        host = Environment.MachineName,
        version = typeof(Program).Assembly.GetName().Version?.ToString(),
        slots = c.Slots.Select(s => new { name = s.Name, description = s.Description, destinations = s.Destinations.Count })
    });
});

app.MapGet("/api/slots/{name}", (HttpContext ctx, string name) =>
{
    var c = (AgentConfig)ctx.Items["cfg"]!;
    var slot = c.Slots.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    return slot == null
        ? Results.Json(new { error = $"Slot \"{name}\" is not configured on {Environment.MachineName}." }, statusCode: 404)
        : Results.Json(new { name = slot.Name, files = Deployer.RequiredFiles(slot) });
});

app.MapPost("/api/deploy", async (HttpContext ctx, DeployRequest req) =>
{
    var c = (AgentConfig)ctx.Items["cfg"]!;
    Log.Write($"Deploy from {ClientIp(ctx)}");
    var result = await Deployer.RunAsync(c, req);
    return Results.Json(result, statusCode: result.Error != null && result.Steps.Count == 0 ? 404 : 200);
});

static string ClientIp(HttpContext ctx)
{
    var a = ctx.Connection.RemoteIpAddress;
    if (a == null) return "";
    return a.IsIPv4MappedToIPv6 ? a.MapToIPv4().ToString() : a.ToString();
}

Log.Write($"CertificateGet Agent listening on https://*:{cfg.Port}  (TLS SHA-256 {AgentConfig.Fingerprint(tls)})");
app.Run();
return 0;

// ---------- CLI helpers ----------
static class Cli
{
    public const string ServiceName = "CertificateGetAgent";

    public static int Install(string[] args)
    {
        var cfg = File.Exists(AgentConfig.FilePath) ? AgentConfig.Load() : AgentConfig.Sample();
        if (args.Length > 1 && int.TryParse(args[1], out var port)) cfg.Port = port;
        string? key = null;
        if (!cfg.HasValidKeyHash) key = cfg.NewApiKey();
        cfg.Save();
        using var tls = cfg.LoadOrCreateTlsCertificate();

        if (OperatingSystem.IsWindows())
        {
            var exe = Environment.ProcessPath!;
            Exec("sc.exe", $"stop {ServiceName}", quiet: true);
            Exec("sc.exe", $"delete {ServiceName}", quiet: true);
            Thread.Sleep(1000);
            Exec("sc.exe", $"create {ServiceName} binPath= \"\\\"{exe}\\\" run\" start= delayed-auto DisplayName= \"CertificateGet Agent\"");
            Exec("sc.exe", $"description {ServiceName} \"Receives renewed certificates from CertificateGet and installs them.\"");
            Exec("sc.exe", $"failure {ServiceName} reset= 86400 actions= restart/60000/restart/60000/restart/60000");
            Exec("netsh", $"advfirewall firewall delete rule name=\"CertificateGet Agent\"", quiet: true);
            Exec("netsh", $"advfirewall firewall add rule name=\"CertificateGet Agent\" dir=in action=allow protocol=TCP localport={cfg.Port}");
            Exec("sc.exe", $"start {ServiceName}");
        }
        else
        {
            var exe = Environment.ProcessPath!;
            File.WriteAllText("/etc/systemd/system/certificateget-agent.service", $"""
                [Unit]
                Description=CertificateGet Agent
                After=network-online.target

                [Service]
                Type=notify
                ExecStart={exe} run
                WorkingDirectory={AgentConfig.Folder}
                Restart=on-failure

                [Install]
                WantedBy=multi-user.target
                """);
            Exec("systemctl", "daemon-reload");
            Exec("systemctl", "enable --now certificateget-agent");
            Exec("systemctl", "restart certificateget-agent");
        }

        Console.WriteLine();
        Console.WriteLine("Installed. Enter these in CertificateGet → certificate → Deployment → Add target → Agent:");
        Console.WriteLine($"  URL          https://{Environment.MachineName}:{cfg.Port}");
        if (key != null) Console.WriteLine($"  API key      {key}      (shown once — store it now)");
        else Console.WriteLine("  API key      unchanged (run \"newkey\" to create a new one)");
        Console.WriteLine($"  Fingerprint  {AgentConfig.Fingerprint(tls)}   (the app asks you to confirm this on first connect)");
        Console.WriteLine();
        Console.WriteLine($"Edit {AgentConfig.FilePath} to define slots and destinations. Changes apply immediately; run \"check\" to validate.");
        if (!OperatingSystem.IsWindows()) Console.WriteLine($"Open TCP port {cfg.Port} in the firewall if needed (e.g. ufw allow {cfg.Port}/tcp).");
        return 0;
    }

    public static int Uninstall()
    {
        if (OperatingSystem.IsWindows())
        {
            Exec("sc.exe", $"stop {ServiceName}", quiet: true);
            Exec("sc.exe", $"delete {ServiceName}");
            Exec("netsh", "advfirewall firewall delete rule name=\"CertificateGet Agent\"", quiet: true);
        }
        else
        {
            Exec("systemctl", "disable --now certificateget-agent", quiet: true);
            try { File.Delete("/etc/systemd/system/certificateget-agent.service"); } catch { }
            Exec("systemctl", "daemon-reload");
        }
        Console.WriteLine("Service removed. agent.json, the TLS certificate and backups were kept.");
        return 0;
    }

    public static int NewKey()
    {
        var cfg = AgentConfig.Load();
        var key = cfg.NewApiKey();
        cfg.Save();
        Console.WriteLine($"New API key: {key}");
        Console.WriteLine("Update it in CertificateGet for every target that uses this agent.");
        return 0;
    }

    public static int Info()
    {
        var cfg = AgentConfig.Load();
        using var tls = cfg.LoadOrCreateTlsCertificate();
        Console.WriteLine($"URL          https://{Environment.MachineName}:{cfg.Port}");
        Console.WriteLine($"Fingerprint  {AgentConfig.Fingerprint(tls)}");
        Console.WriteLine($"Config       {AgentConfig.FilePath}");
        Console.WriteLine($"Allowed IPs  {(cfg.AllowedIps.Count == 0 ? "any" : string.Join(", ", cfg.AllowedIps))}");
        foreach (var s in cfg.Slots)
        {
            Console.WriteLine($"Slot \"{s.Name}\": {s.Destinations.Count} destination(s), needs {string.Join(", ", Deployer.RequiredFiles(s))}");
            foreach (var d in s.Destinations)
                Console.WriteLine($"   - {d.Name}: {(d.Kind.Equals("TSplus", StringComparison.OrdinalIgnoreCase) ? "TSplus import" : d.Kind.Equals("TSplusJks", StringComparison.OrdinalIgnoreCase) ? "TSplus cert.jks" : d.Folder)}");
        }
        return 0;
    }

    private static readonly HashSet<string> KnownSources = new(StringComparer.OrdinalIgnoreCase)
        { "combined", "combined-keyfirst", "fullchain", "fullchain-root", "cer", "crt", "der", "chain", "key", "encrypted-key", "pfx", "p12", "p7b", "k8s", "jks" };

    public static int Check()
    {
        AgentConfig cfg;
        try { cfg = AgentConfig.Load(); }
        catch (Exception ex) { Console.WriteLine("ERROR " + ex.Message); return 1; }
        var problems = 0;
        void Bad(string m) { Console.WriteLine("  ✗ " + m); problems++; }
        if (!cfg.HasValidKeyHash) Bad("No valid API key (ApiKeyHash is empty or a placeholder) — run \"newkey\" and enter the new key in the app.");
        foreach (var ip in cfg.AllowedIps.Where(ip => !IPAddress.TryParse(ip, out _))) Bad($"AllowedIps: \"{ip}\" is not an IP address.");
        foreach (var dup in cfg.Slots.GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)) Bad($"Slot name \"{dup.Key}\" is used twice.");
        foreach (var s in cfg.Slots)
        {
            Console.WriteLine($"Slot \"{s.Name}\"");
            if (s.Destinations.Count == 0) Bad("has no destinations");
            foreach (var d in s.Destinations)
            {
                var label = string.IsNullOrWhiteSpace(d.Name) ? d.Folder : d.Name;
                if (d.Kind.Equals("TSplus", StringComparison.OrdinalIgnoreCase))
                {
                    var folder = string.IsNullOrWhiteSpace(d.TsplusCertFolder) ? Deployer.DefaultTsplusCertFolder : d.TsplusCertFolder!;
                    if (!File.Exists(Path.Combine(folder, "CertificateManager.exe"))) Bad($"{label}: TSplus CertificateManager.exe not found in {folder}");
                }
                else if (d.Kind.Equals("TSplusJks", StringComparison.OrdinalIgnoreCase))
                {
                    var root = string.IsNullOrWhiteSpace(d.TsplusFolder) ? Deployer.DefaultTsplusFolder : d.TsplusFolder!;
                    if (!Directory.Exists(Path.Combine(root, "Clients", "webserver"))) Bad($"{label}: TSplus web server folder not found: {Path.Combine(root, "Clients", "webserver")}");
                    else if (!File.Exists(Path.Combine(root, "UserDesktop", "files", "AdminTool.exe"))) Console.WriteLine($"  ! {label}: AdminTool.exe not found — the web server will not be restarted automatically");
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(d.Folder)) Bad($"{label}: Folder is empty");
                    else if (!Directory.Exists(d.Folder)) Console.WriteLine($"  ! {label}: folder does not exist yet — it will be created on the first deployment: {d.Folder}");
                    if (d.Files.Count == 0) Bad($"{label}: no Files");
                    foreach (var f in d.Files)
                    {
                        if (!KnownSources.Contains(f.Source)) Bad($"{label}: unknown Source \"{f.Source}\" (use {string.Join(", ", KnownSources)})");
                        if (string.IsNullOrWhiteSpace(f.FileName)) Bad($"{label}: a file has no FileName");
                    }
                }
                if (d.RestartServices.Any(string.IsNullOrWhiteSpace) || d.Commands.Any(string.IsNullOrWhiteSpace) || d.RestartPrograms.Any(p => string.IsNullOrWhiteSpace(p.Path)))
                    Console.WriteLine($"  ! {label}: has empty entries (e.g. \"RestartServices\": [\"\"]) — they are ignored; use [] for an empty list.");
                foreach (var prog in d.RestartPrograms.Where(p => !string.IsNullOrWhiteSpace(p.Path)))
                {
                    if (!OperatingSystem.IsWindows()) Bad($"{label}: RestartPrograms only works on Windows (use Commands)");
                    else if (!File.Exists(prog.Path)) Bad($"{label}: program not found: {prog.Path}");
                    if (prog.StartIn.ToLowerInvariant() is not ("samesession" or "console" or "background"))
                        Bad($"{label}: StartIn must be SameSession, Console or Background (is \"{prog.StartIn}\")");
                }
                if (OperatingSystem.IsWindows())
                    foreach (var svc in d.RestartServices.Where(s => !string.IsNullOrWhiteSpace(s)))
                        if (!ServiceExists(svc)) Bad($"{label}: Windows service \"{svc}\" not found");
                Console.WriteLine($"  • {label}");
            }
        }
        Console.WriteLine(problems == 0 ? "OK — configuration looks good." : $"{problems} problem(s) found.");
        return problems == 0 ? 0 : 1;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool ServiceExists(string name)
    {
        try { using var sc = new System.ServiceProcess.ServiceController(name); _ = sc.Status; return true; }
        catch { return false; }
    }

    private static void Exec(string file, string args, bool quiet = false)
    {
        var psi = new ProcessStartInfo(file, args) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        using var p = Process.Start(psi)!;
        var output = (p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd()).Trim();
        p.WaitForExit();
        if (!quiet && p.ExitCode != 0) Console.WriteLine($"  {file} {args}\n  -> exit {p.ExitCode}: {output}");
    }
}
