using System.Net;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using CertificateGet.Models;
using CertificateGet.Services;
using CertificateGet.UI;
using Microsoft.Win32;

namespace CertificateGet.Views;

public partial class SettingsView : UserControl
{
    public class FormatChoice
    {
        public string Id { get; init; } = "";
        public string Label { get; init; } = "";
        public string Description { get; init; } = "";
        public string FileName { get; init; } = "";
        public bool Selected { get; set; }
        public bool Editable { get; init; }
    }

    private List<FormatChoice> _formatChoices = new();

    private const string TokenUnchanged = "••••••••";

    /// <summary>acme-dns registrations removed on this page; applied on Save (ones registered meanwhile are kept).</summary>
    private readonly HashSet<string> _removedAcmeDns = new(StringComparer.OrdinalIgnoreCase);

    public SettingsView() => InitializeComponent();

    public void Load()
    {
        var s = SettingsService.Current;
        StoreBox.Text = s.StorePath;
        EmailBox.Text = s.DefaultEmail ?? "";
        EnvStaging.IsChecked = s.DefaultEnvironment == AcmeEnvironment.Staging;
        EnvProduction.IsChecked = s.DefaultEnvironment == AcmeEnvironment.Production;
        foreach (ComboBoxItem i in KeyTypeBox.Items)
            if ((string)i.Tag == s.DefaultKeyType.ToString()) KeyTypeBox.SelectedItem = i;
        WarnDaysBox.Text = s.RenewWarningDays.ToString();
        KeyPkcs8.IsChecked = s.KeyFormatPkcs8;
        KeyTraditional.IsChecked = !s.KeyFormatPkcs8;
        PfxLegacy.IsChecked = s.PfxLegacyEncryption;
        PfxAes.IsChecked = !s.PfxLegacyEncryption;
        JksPasswordBox.Text = s.JksPassword;
        CfTokenBox.Password = s.ProtectedCloudflareToken != null ? TokenUnchanged : "";
        HostingerTokenBox.Password = s.ProtectedHostingerToken != null ? TokenUnchanged : "";
        ConstellixApiKeyBox.Password = s.ProtectedConstellixApiKey != null ? TokenUnchanged : "";
        ConstellixSecretBox.Password = s.ProtectedConstellixSecretKey != null ? TokenUnchanged : "";
        DmeApiKeyBox.Password = s.ProtectedDnsMadeEasyApiKey != null ? TokenUnchanged : "";
        DmeSecretBox.Password = s.ProtectedDnsMadeEasySecretKey != null ? TokenUnchanged : "";
        NamecheapUserBox.Text = s.NamecheapApiUser ?? "";
        NamecheapKeyBox.Password = s.ProtectedNamecheapApiKey != null ? TokenUnchanged : "";
        NamecheapIpBox.Text = s.NamecheapClientIp ?? "";
        AcmeDnsServerBox.Text = s.AcmeDnsServer;
        _removedAcmeDns.Clear();
        ShowAcmeDnsAccounts();
        ResolversBox.Text = s.DnsResolvers;
        var chosen = CertFileKind.ForIssuance().ToHashSet();
        _formatChoices = CertFileKind.All.Select(k => new FormatChoice
        {
            Id = k.Id, Label = k.Label, Description = k.Description, FileName = k.FileNamesDisplay("name"),
            Selected = chosen.Contains(k.Id), Editable = !k.Required
        }).ToList();
        IssueFormatsList.ItemsSource = _formatChoices;
        PropagationBox.Text = s.DnsPropagationTimeoutSeconds.ToString();
        LoadAccounts();
    }

    private void LoadAccounts()
    {
        var lines = new List<string>();
        foreach (var env in new[] { "staging", "production" })
        {
            var f = Path.Combine(SettingsService.AccountsPath, env + ".json");
            if (!File.Exists(f)) { lines.Add($"{Cap(env)}: no account yet (created automatically on first request)."); continue; }
            try
            {
                var rec = JsonSerializer.Deserialize<AcmeAccountRecord>(File.ReadAllText(f), Json.Options);
                lines.Add($"{Cap(env)}: created {rec?.CreatedUtc.ToLocalTime():yyyy-MM-dd}{(string.IsNullOrEmpty(rec?.Email) ? "" : " · " + rec.Email)}");
            }
            catch { lines.Add($"{Cap(env)}: unreadable account file."); }
        }
        AccountsText.Text = string.Join("\n", lines);
        static string Cap(string s) => char.ToUpper(s[0]) + s[1..];
    }

    private void BrowseStore_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose the store folder" };
        if (Directory.Exists(StoreBox.Text)) dlg.InitialDirectory = StoreBox.Text;
        if (dlg.ShowDialog() == true) StoreBox.Text = dlg.FolderName;
    }

    private void OpenStore_Click(object sender, RoutedEventArgs e) => Shell.OpenFolder(SettingsService.StorePath);

    private async void TestToken_Click(object sender, RoutedEventArgs e)
    {
        var token = CfTokenBox.Password == TokenUnchanged
            ? Secret.Unprotect(SettingsService.Current.ProtectedCloudflareToken)
            : CfTokenBox.Password.Trim();
        if (string.IsNullOrEmpty(token))
        {
            Modal.Warning("No token", "Paste a Cloudflare API token first.");
            return;
        }
        try
        {
            using var cf = new CloudflareDns(token);
            var status = await cf.VerifyTokenAsync();
            if (status == "active") Modal.Success("Token works", "Cloudflare reports the token as active.");
            else Modal.Warning("Token not active", $"Cloudflare reports the token status as \"{status}\".");
        }
        catch (Exception ex)
        {
            Modal.Error("Token test failed", ex.Message);
        }
    }

    private async void TestHostinger_Click(object sender, RoutedEventArgs e)
    {
        var token = HostingerTokenBox.Password == TokenUnchanged
            ? Secret.Unprotect(SettingsService.Current.ProtectedHostingerToken)
            : HostingerTokenBox.Password.Trim();
        if (string.IsNullOrEmpty(token))
        {
            Modal.Warning("No token", "Paste a Hostinger API token first.");
            return;
        }
        try
        {
            using var h = new HostingerDns(token);
            var access = await h.CheckDnsAccessAsync();
            var ok = access.Where(a => a.Ok).Select(a => a.Domain).OrderBy(d => d).ToList();
            var bad = access.Where(a => !a.Ok).OrderBy(a => a.Domain).ToList();
            if (access.Count == 0)
                Modal.Warning("Token works, no domains", "The token was accepted, but no domains were found in this Hostinger account.");
            else if (bad.Count == 0)
                Modal.Success("Token works", $"Hostinger accepted the token and allows DNS editing for:\n\n{string.Join("\n", ok)}");
            else
            {
                var text = (ok.Count > 0 ? $"DNS editing allowed for:\n{string.Join("\n", ok)}\n\n" : "") +
                           $"DNS editing refused for:\n{string.Join("\n", bad.Select(b => b.Domain))}\n\n" +
                           $"Hostinger says: {bad[0].Error}";
                Modal.Warning(ok.Count == 0 ? "Token cannot edit DNS" : "Token has partial DNS access", text);
            }
        }
        catch (Exception ex)
        {
            Modal.Error("Token test failed", ex.Message);
        }
    }

    private async void TestConstellix_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = ConstellixApiKeyBox.Password == TokenUnchanged
            ? Secret.Unprotect(SettingsService.Current.ProtectedConstellixApiKey)
            : ConstellixApiKeyBox.Password.Trim();
        var secret = ConstellixSecretBox.Password == TokenUnchanged
            ? Secret.Unprotect(SettingsService.Current.ProtectedConstellixSecretKey)
            : ConstellixSecretBox.Password.Trim();
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secret))
        {
            Modal.Warning("Keys missing", "Enter both the Constellix API key and the secret key.");
            return;
        }
        try
        {
            using var c = new ConstellixDns(apiKey, secret);
            var domains = await c.ListDomainsAsync();
            if (domains.Count == 0)
                Modal.Warning("Keys work, no domains", "Constellix accepted the keys, but no domains were found in this account.");
            else
                Modal.Success("Keys work", $"Constellix accepted the keys. Domains in this account ({domains.Count}):\n\n{string.Join("\n", domains.OrderBy(d => d).Take(40))}" +
                                           (domains.Count > 40 ? $"\n… and {domains.Count - 40} more" : ""));
        }
        catch (Exception ex)
        {
            Modal.Error("Constellix test failed", ex.Message);
        }
    }

    private static string? Pick(PasswordBox box, string? stored) =>
        box.Password == TokenUnchanged ? Secret.Unprotect(stored) : box.Password.Trim();

    private async void TestDnsMadeEasy_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = Pick(DmeApiKeyBox, SettingsService.Current.ProtectedDnsMadeEasyApiKey);
        var secret = Pick(DmeSecretBox, SettingsService.Current.ProtectedDnsMadeEasySecretKey);
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(secret))
        {
            Modal.Warning("Keys missing", "Enter both the DNS Made Easy API key and the secret key.");
            return;
        }
        try
        {
            using var c = new DnsMadeEasyDns(apiKey, secret);
            var domains = await c.ListDomainsAsync();
            if (domains.Count == 0)
                Modal.Warning("Keys work, no domains", "DNS Made Easy accepted the keys, but no managed domains were found in this account.");
            else
                Modal.Success("Keys work", $"DNS Made Easy accepted the keys. Managed domains ({domains.Count}):\n\n{string.Join("\n", domains.OrderBy(d => d).Take(40))}" +
                                           (domains.Count > 40 ? $"\n… and {domains.Count - 40} more" : ""));
        }
        catch (Exception ex)
        {
            Modal.Error("DNS Made Easy test failed", ex.Message);
        }
    }

    private async void TestNamecheap_Click(object sender, RoutedEventArgs e)
    {
        var user = NamecheapUserBox.Text.Trim();
        var key = Pick(NamecheapKeyBox, SettingsService.Current.ProtectedNamecheapApiKey);
        var ip = NamecheapIpBox.Text.Trim();
        if (user.Length == 0 || string.IsNullOrEmpty(key))
        {
            Modal.Warning("API access missing", "Enter the Namecheap API user (your account user name) and the API key.");
            return;
        }
        if (ip.Length > 0 && !IPAddress.TryParse(ip, out _))
        {
            Modal.Warning("Invalid client IP", "Enter this PC's public IPv4 address, or leave the field empty to detect it.");
            return;
        }
        try
        {
            using var c = new NamecheapDns(user, key, ip);
            var domains = await c.ListDomainsAsync();
            var usedIp = await c.ClientIpAsync();
            if (domains.Count == 0)
                Modal.Warning("API access works, no domains", $"Namecheap accepted the key from {usedIp}, but no domains were found in this account.");
            else
                Modal.Success("API access works", $"Namecheap accepted the key from {usedIp}. Domains ({domains.Count}):\n\n{string.Join("\n", domains.OrderBy(d => d).Take(40))}" +
                                                 (domains.Count > 40 ? $"\n… and {domains.Count - 40} more" : ""));
        }
        catch (Exception ex)
        {
            Modal.Error("Namecheap test failed", ex.Message);
        }
    }

    private async void TestAcmeDns_Click(object sender, RoutedEventArgs e)
    {
        var server = AcmeDnsServerBox.Text.Trim();
        if (server.Length == 0) { Modal.Warning("Server missing", "Enter the acme-dns server URL."); return; }
        try
        {
            await AcmeDnsProvider.CheckHealthAsync(server);
            Modal.Success("Server reachable", $"{AcmeDnsProvider.NormalizeServer(server)} answers. Domains are registered on it on their first request.");
        }
        catch (Exception ex)
        {
            Modal.Error("acme-dns test failed", ex.Message);
        }
    }

    private void ShowAcmeDnsAccounts()
    {
        var list = SettingsService.Current.AcmeDnsAccounts.Where(a => !_removedAcmeDns.Contains(a.Domain)).OrderBy(a => a.Domain).ToList();
        AcmeDnsList.ItemsSource = list;
        AcmeDnsEmpty.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void CopyAcmeDns_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AcmeDnsAccount a) Shell.CopyToClipboard(a.FullDomain);
    }

    private void RemoveAcmeDns_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not AcmeDnsAccount a) return;
        if (!Modal.Confirm("Remove acme-dns registration",
                $"Forget the acme-dns registration for {a.Domain}? The next request registers it again with a new name, and the " +
                $"CNAME for {a.CnameName} must then be changed. Takes effect when you save.", "Remove", "Cancel", danger: true)) return;
        _removedAcmeDns.Add(a.Domain);
        ShowAcmeDnsAccounts();
    }

    private void ResetAccounts_Click(object sender, RoutedEventArgs e)
    {
        if (!Modal.Confirm("Reset Let's Encrypt accounts",
                "Delete the stored ACME account keys? New accounts are created on the next request. Issued certificates are not affected.",
                "Reset", "Cancel", danger: true)) return;
        foreach (var f in Directory.GetFiles(SettingsService.AccountsPath, "*.json")) File.Delete(f);
        ActivityLog.Warning("Account", "Stored Let's Encrypt accounts were reset.");
        LoadAccounts();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(WarnDaysBox.Text, out var warn) || warn is < 1 or > 90)
        {
            Modal.Warning("Invalid value", "Warning days must be a number between 1 and 90.");
            return;
        }
        if (!int.TryParse(PropagationBox.Text, out var prop) || prop is < 30 or > 3600)
        {
            Modal.Warning("Invalid value", "DNS propagation wait must be between 30 and 3600 seconds.");
            return;
        }
        var resolvers = ResolversBox.Text.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (resolvers.Length == 0 || resolvers.Any(r => !IPAddress.TryParse(r, out _)))
        {
            Modal.Warning("Invalid resolvers", "Enter one or more IP addresses separated by commas, e.g. 1.1.1.1, 8.8.8.8");
            return;
        }
        var ncIp = NamecheapIpBox.Text.Trim();
        if (ncIp.Length > 0 && !IPAddress.TryParse(ncIp, out _))
        {
            Modal.Warning("Invalid client IP", "Enter this PC's public IPv4 address for Namecheap, or leave the field empty to detect it.");
            return;
        }
        var acmeDnsServer = AcmeDnsServerBox.Text.Trim();
        if (acmeDnsServer.Length == 0) acmeDnsServer = "https://auth.acme-dns.io";
        if (!Uri.TryCreate(AcmeDnsProvider.NormalizeServer(acmeDnsServer), UriKind.Absolute, out _))
        {
            Modal.Warning("Invalid acme-dns server", "Enter the acme-dns server URL, for example https://auth.acme-dns.io");
            return;
        }
        var store = StoreBox.Text.Trim();
        try
        {
            Directory.CreateDirectory(store);
        }
        catch (Exception ex)
        {
            Modal.Error("Store folder", $"Cannot use this folder:\n{ex.Message}");
            return;
        }

        var old = SettingsService.Current;
        var s = new AppSettings
        {
            StorePath = store,
            DefaultEmail = string.IsNullOrWhiteSpace(EmailBox.Text) ? null : EmailBox.Text.Trim(),
            DefaultEnvironment = EnvProduction.IsChecked == true ? AcmeEnvironment.Production : AcmeEnvironment.Staging,
            DefaultKeyType = Enum.Parse<CertKeyType>((string)((ComboBoxItem)KeyTypeBox.SelectedItem).Tag),
            RenewWarningDays = warn,
            KeyFormatPkcs8 = KeyPkcs8.IsChecked == true,
            PfxLegacyEncryption = PfxLegacy.IsChecked == true,
            JksPassword = string.IsNullOrWhiteSpace(JksPasswordBox.Text) ? "secret" : JksPasswordBox.Text,
            ProtectedCloudflareToken = CfTokenBox.Password == TokenUnchanged
                ? old.ProtectedCloudflareToken
                : Secret.Protect(CfTokenBox.Password.Trim()),
            ProtectedHostingerToken = HostingerTokenBox.Password == TokenUnchanged
                ? old.ProtectedHostingerToken
                : Secret.Protect(HostingerTokenBox.Password.Trim()),
            ProtectedConstellixApiKey = ConstellixApiKeyBox.Password == TokenUnchanged
                ? old.ProtectedConstellixApiKey
                : Secret.Protect(ConstellixApiKeyBox.Password.Trim()),
            ProtectedConstellixSecretKey = ConstellixSecretBox.Password == TokenUnchanged
                ? old.ProtectedConstellixSecretKey
                : Secret.Protect(ConstellixSecretBox.Password.Trim()),
            ProtectedDnsMadeEasyApiKey = DmeApiKeyBox.Password == TokenUnchanged
                ? old.ProtectedDnsMadeEasyApiKey
                : Secret.Protect(DmeApiKeyBox.Password.Trim()),
            ProtectedDnsMadeEasySecretKey = DmeSecretBox.Password == TokenUnchanged
                ? old.ProtectedDnsMadeEasySecretKey
                : Secret.Protect(DmeSecretBox.Password.Trim()),
            NamecheapApiUser = string.IsNullOrWhiteSpace(NamecheapUserBox.Text) ? null : NamecheapUserBox.Text.Trim(),
            ProtectedNamecheapApiKey = NamecheapKeyBox.Password == TokenUnchanged
                ? old.ProtectedNamecheapApiKey
                : Secret.Protect(NamecheapKeyBox.Password.Trim()),
            NamecheapClientIp = ncIp.Length == 0 ? null : ncIp,
            AcmeDnsServer = AcmeDnsProvider.NormalizeServer(acmeDnsServer),
            AcmeDnsAccounts = old.AcmeDnsAccounts.Where(a => !_removedAcmeDns.Contains(a.Domain)).ToList(),
            IssueFormats = _formatChoices.Where(c => c.Selected && c.Editable).Select(c => c.Id).ToList(),
            DnsResolvers = string.Join(", ", resolvers),
            DnsPropagationTimeoutSeconds = prop
        };
        SettingsService.Save(s);
        _removedAcmeDns.Clear();
        ShowAcmeDnsAccounts();
        ActivityLog.Info("Settings", "Settings saved." + (old.StorePath != s.StorePath ? $" Store moved to {s.StorePath}." : ""));
        MainWindow.Instance?.UpdateStorePath();
        CertificateStore.RaiseChanged();
        LoadAccounts();
        Modal.Success("Settings saved", old.StorePath != s.StorePath
            ? "Settings saved. The app now uses the new store folder — existing certificates in the old folder were not moved."
            : "Your settings have been saved.");
    }
}
