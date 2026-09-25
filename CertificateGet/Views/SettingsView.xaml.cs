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
        CfTokenBox.Password = s.ProtectedCloudflareToken != null ? TokenUnchanged : "";
        HostingerTokenBox.Password = s.ProtectedHostingerToken != null ? TokenUnchanged : "";
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
            ProtectedCloudflareToken = CfTokenBox.Password == TokenUnchanged
                ? old.ProtectedCloudflareToken
                : Secret.Protect(CfTokenBox.Password.Trim()),
            ProtectedHostingerToken = HostingerTokenBox.Password == TokenUnchanged
                ? old.ProtectedHostingerToken
                : Secret.Protect(HostingerTokenBox.Password.Trim()),
            IssueFormats = _formatChoices.Where(c => c.Selected && c.Editable).Select(c => c.Id).ToList(),
            DnsResolvers = string.Join(", ", resolvers),
            DnsPropagationTimeoutSeconds = prop
        };
        SettingsService.Save(s);
        ActivityLog.Info("Settings", "Settings saved." + (old.StorePath != s.StorePath ? $" Store moved to {s.StorePath}." : ""));
        MainWindow.Instance?.UpdateStorePath();
        CertificateStore.RaiseChanged();
        LoadAccounts();
        Modal.Success("Settings saved", old.StorePath != s.StorePath
            ? "Settings saved. The app now uses the new store folder — existing certificates in the old folder were not moved."
            : "Your settings have been saved.");
    }
}
