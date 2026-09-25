using System.ComponentModel;
using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace CertificateGet.Agent;

/// <summary>A desktop program (not a service) to restart after the certificate files were written.</summary>
public class ProgramSpec
{
    /// <summary>Full path of the .exe.</summary>
    public string Path { get; set; } = "";
    /// <summary>Arguments used only when the program was not running (otherwise each instance keeps its own command line).</summary>
    public string Arguments { get; set; } = "";
    /// <summary>Working folder for the new process; default = the exe's folder.</summary>
    public string? WorkingFolder { get; set; }
    /// <summary>Seconds to wait for a normal close before the process is ended forcefully.</summary>
    public int StopTimeoutSeconds { get; set; } = 20;
    /// <summary>SameSession (default): restart on the desktop it was running on (or the console if it was not running).
    /// Console: always on the console user's desktop. Background: session 0 as the agent's account (no desktop).</summary>
    public string StartIn { get; set; } = "SameSession";
}

/// <summary>Stops and restarts desktop programs, putting them back into the user session they were running in.</summary>
[SupportedOSPlatform("windows")]
public static class ProgramRestarter
{
    private record Running(int Pid, int SessionId, string CommandLine, string? User);

    public static async Task<string> RestartAsync(ProgramSpec spec)
    {
        var exe = System.IO.Path.GetFullPath(spec.Path);
        if (!File.Exists(exe)) throw new FileNotFoundException($"Program not found: {exe}");
        var workDir = string.IsNullOrWhiteSpace(spec.WorkingFolder) ? System.IO.Path.GetDirectoryName(exe)! : spec.WorkingFolder!;
        var name = System.IO.Path.GetFileName(exe);

        // 1. What is running now (every instance, with its own command line and session).
        var running = FindRunning(exe);

        // 2. Ask each instance to close, then end the ones that do not.
        foreach (var r in running) RequestClose(r);
        var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, spec.StopTimeoutSeconds));
        var forced = 0;
        foreach (var r in running)
        {
            try
            {
                using var p = Process.GetProcessById(r.Pid);
                var left = deadline - DateTime.UtcNow;
                if (left < TimeSpan.Zero || !p.WaitForExit(left))
                {
                    p.Kill();
                    p.WaitForExit(10_000);
                    forced++;
                }
            }
            catch (ArgumentException) { /* already exited */ }
        }

        // 3. Start again: each previous instance with its own command line, in its own session.
        var starts = running.Count > 0
            ? running.Select(r => (Session: r.SessionId, CommandLine: r.CommandLine, r.User)).ToList()
            : new List<(int Session, string CommandLine, string? User)> { (-1, Quote(exe) + (spec.Arguments.Length > 0 ? " " + spec.Arguments : ""), null) };

        var started = new List<(int Pid, int Session)>();
        foreach (var s in starts)
        {
            var session = spec.StartIn.ToLowerInvariant() switch
            {
                "background" => 0,
                "console" => (int)WTSGetActiveConsoleSessionId(),
                _ => s.Session > 0 ? s.Session : (int)WTSGetActiveConsoleSessionId()
            };
            var cmd = string.IsNullOrWhiteSpace(s.CommandLine) ? Quote(exe) : s.CommandLine;
            started.Add((StartInSession(session, exe, cmd, workDir), session));
        }

        // 4. Make sure they stay up.
        await Task.Delay(3000);
        var died = started.Where(s => { try { using var p = Process.GetProcessById(s.Pid); return p.HasExited; } catch { return true; } }).ToList();
        if (died.Count > 0)
            throw new InvalidOperationException($"{name} was started but exited again within 3 seconds (session {died[0].Session}). Check the program's own log.");

        var sessions = string.Join(", ", started.Select(s => s.Session).Distinct());
        return $"Restarted {name}: {running.Count} instance(s) stopped{(forced > 0 ? $" ({forced} forcefully)" : "")}, " +
               $"{started.Count} started in session {sessions}";
    }

    private static List<Running> FindRunning(string exe)
    {
        var list = new List<Running>();
        var escaped = exe.Replace("\\", "\\\\").Replace("'", "\\'");
        using var searcher = new ManagementObjectSearcher(
            $"SELECT ProcessId, SessionId, CommandLine FROM Win32_Process WHERE ExecutablePath = '{escaped}'");
        foreach (ManagementObject mo in searcher.Get())
        {
            using (mo)
            {
                string? user = null;
                try
                {
                    var owner = new object[2];
                    if (Convert.ToInt32(mo.InvokeMethod("GetOwner", owner)) == 0) user = $"{owner[1]}\\{owner[0]}";
                }
                catch { /* owner is informational only */ }
                list.Add(new Running(Convert.ToInt32(mo["ProcessId"]), Convert.ToInt32(mo["SessionId"]), mo["CommandLine"] as string ?? "", user));
            }
        }
        return list;
    }

    /// <summary>A normal close (WM_CLOSE). Window messages cannot cross sessions, so for a program on another
    /// desktop we run "taskkill /PID n" (without /F) inside that session.</summary>
    private static void RequestClose(Running r)
    {
        try
        {
            if (r.SessionId == Process.GetCurrentProcess().SessionId)
            {
                using var p = Process.GetProcessById(r.Pid);
                p.CloseMainWindow();
            }
            else
            {
                var taskkill = System.IO.Path.Combine(Environment.SystemDirectory, "taskkill.exe");
                StartInSession(r.SessionId, taskkill, $"{Quote(taskkill)} /PID {r.Pid}", Environment.SystemDirectory, hidden: true);
            }
        }
        catch { /* falls back to Kill after the timeout */ }
    }

    private static string Quote(string path) => "\"" + path + "\"";

    /// <summary>Starts a process on the desktop of the given session, as the user logged on there.</summary>
    private static int StartInSession(int sessionId, string exe, string commandLine, string workDir, bool hidden = false)
    {
        // Same session as the agent (e.g. running "run" in a console, or Background mode): a normal start.
        if (sessionId == Process.GetCurrentProcess().SessionId)
        {
            var psi = new ProcessStartInfo(exe, SplitArgs(commandLine, exe))
            {
                WorkingDirectory = workDir, UseShellExecute = false, CreateNoWindow = hidden
            };
            using var p = Process.Start(psi) ?? throw new InvalidOperationException($"Could not start {exe}");
            return p.Id;
        }

        if (!WTSQueryUserToken((uint)sessionId, out var userToken))
            throw new Win32Exception(Marshal.GetLastWin32Error(),
                $"Cannot start the program in session {sessionId}: no user is logged on there, or the agent is not running as the Windows service.");
        IntPtr primary = IntPtr.Zero, env = IntPtr.Zero;
        try
        {
            if (!DuplicateTokenEx(userToken, MAXIMUM_ALLOWED, IntPtr.Zero, SecurityImpersonation, TokenPrimary, out primary))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "DuplicateTokenEx failed");
            if (!CreateEnvironmentBlock(out env, primary, false)) env = IntPtr.Zero;

            var si = new STARTUPINFO
            {
                cb = Marshal.SizeOf<STARTUPINFO>(),
                lpDesktop = @"winsta0\default",
                dwFlags = hidden ? STARTF_USESHOWWINDOW : 0,
                wShowWindow = 0
            };
            var cmd = new StringBuilder(commandLine, commandLine.Length + 1);
            var flags = CREATE_UNICODE_ENVIRONMENT | (hidden ? CREATE_NO_WINDOW : CREATE_NEW_CONSOLE);
            if (!CreateProcessAsUser(primary, exe, cmd, IntPtr.Zero, IntPtr.Zero, false, flags, env, workDir, ref si, out var pi))
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"CreateProcessAsUser failed for {exe}");
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return pi.dwProcessId;
        }
        finally
        {
            if (env != IntPtr.Zero) DestroyEnvironmentBlock(env);
            if (primary != IntPtr.Zero) CloseHandle(primary);
            CloseHandle(userToken);
        }
    }

    /// <summary>Removes the executable part from a full command line.</summary>
    private static string SplitArgs(string commandLine, string exe)
    {
        var c = commandLine.TrimStart();
        if (c.StartsWith('"'))
        {
            var end = c.IndexOf('"', 1);
            return end < 0 ? "" : c[(end + 1)..].TrimStart();
        }
        var space = c.IndexOf(' ');
        return space < 0 ? "" : c[(space + 1)..].TrimStart();
    }

    // ---------- Win32 ----------
    private const uint MAXIMUM_ALLOWED = 0x02000000;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;
    private const uint CREATE_UNICODE_ENVIRONMENT = 0x00000400;
    private const uint CREATE_NEW_CONSOLE = 0x00000010;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private const int STARTF_USESHOWWINDOW = 0x00000001;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved;
        public string? lpDesktop;
        public string? lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public IntPtr lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public IntPtr hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("kernel32.dll")] private static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("wtsapi32.dll", SetLastError = true)] private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(IntPtr existing, uint access, IntPtr attributes, int impersonationLevel, int tokenType, out IntPtr newToken);
    [DllImport("userenv.dll", SetLastError = true)] private static extern bool CreateEnvironmentBlock(out IntPtr env, IntPtr token, bool inherit);
    [DllImport("userenv.dll", SetLastError = true)] private static extern bool DestroyEnvironmentBlock(IntPtr env);
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(IntPtr token, string applicationName, StringBuilder commandLine, IntPtr processAttributes,
        IntPtr threadAttributes, bool inheritHandles, uint creationFlags, IntPtr environment, string currentDirectory,
        ref STARTUPINFO startupInfo, out PROCESS_INFORMATION processInformation);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
}
