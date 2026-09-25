using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CertificateGet.Models;

namespace CertificateGet.Services;

/// <summary>DPAPI helpers — secrets can only be decrypted by the same Windows user on the same machine.</summary>
public static class Secret
{
    public static string? Protect(string? plain)
    {
        if (string.IsNullOrEmpty(plain)) return null;
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(bytes);
    }

    public static string? Unprotect(string? protectedText)
    {
        if (string.IsNullOrEmpty(protectedText)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(protectedText), null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static readonly JsonSerializerOptions Compact = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

public static class SettingsService
{
    private static readonly string SettingsFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CertificateGet", "settings.json");

    public static AppSettings Current { get; private set; } = Load();

    public static string StorePath => Current.StorePath;
    public static string CertificatesPath => Path.Combine(StorePath, "certificates");
    public static string AccountsPath => Path.Combine(StorePath, "accounts");
    public static string LogFile => Path.Combine(StorePath, "activity.jsonl");

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsFile), Json.Options) ?? new AppSettings();
        }
        catch { /* fall back to defaults */ }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, Json.Options));
        Current = settings;
        EnsureStore();
    }

    public static void EnsureStore()
    {
        Directory.CreateDirectory(CertificatesPath);
        Directory.CreateDirectory(AccountsPath);
    }
}

/// <summary>Append-only activity journal stored as JSON lines next to the certificates.</summary>
public static class ActivityLog
{
    private static readonly object Sync = new();

    public static event Action<LogEntry>? EntryAdded;

    public static void Info(string category, string message, string? cert = null) => Write(LogLevel.Info, category, message, cert);
    public static void Success(string category, string message, string? cert = null) => Write(LogLevel.Success, category, message, cert);
    public static void Warning(string category, string message, string? cert = null) => Write(LogLevel.Warning, category, message, cert);
    public static void Error(string category, string message, string? cert = null) => Write(LogLevel.Error, category, message, cert);

    public static void Write(LogLevel level, string category, string message, string? cert = null)
    {
        var entry = new LogEntry { Timestamp = DateTime.Now, Level = level, Category = category, Certificate = cert, Message = message };
        lock (Sync)
        {
            try
            {
                SettingsService.EnsureStore();
                File.AppendAllText(SettingsService.LogFile, JsonSerializer.Serialize(entry, Json.Compact) + Environment.NewLine);
            }
            catch { /* logging must never break the app */ }
        }
        EntryAdded?.Invoke(entry);
    }

    public static List<LogEntry> ReadAll()
    {
        var list = new List<LogEntry>();
        lock (Sync)
        {
            if (!File.Exists(SettingsService.LogFile)) return list;
            foreach (var line in File.ReadAllLines(SettingsService.LogFile))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var e = JsonSerializer.Deserialize<LogEntry>(line, Json.Compact);
                    if (e != null) list.Add(e);
                }
                catch { /* skip corrupt line */ }
            }
        }
        return list;
    }
}
