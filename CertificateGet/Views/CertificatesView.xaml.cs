using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Controls;
using CertificateGet.Models;
using CertificateGet.Services;
using CertificateGet.UI;

namespace CertificateGet.Views;

public partial class CertificatesView : UserControl
{
    public class FileRow
    {
        public string Name { get; init; } = "";
        public string Ext { get; init; } = "";
        public string Label { get; init; } = "";
        public string Description { get; init; } = "";
        public string FullPath { get; init; } = "";
    }

    private List<CertificateProfile> _all = new();
    private bool _deploying;

    public CertificatesView()
    {
        InitializeComponent();
        SearchBox.Tag = "Search by name or domain…";
        CertificateStore.Changed += () => Dispatcher.BeginInvoke(() => Reload((CertList.SelectedItem as CertificateProfile)?.Id));
    }

    private CertificateProfile? Selected => CertList.SelectedItem as CertificateProfile;

    public void Reload(string? selectId = null)
    {
        selectId ??= Selected?.Id;
        _all = CertificateStore.LoadAll();
        var warn = SettingsService.Current.RenewWarningDays;
        StatTotal.Text = _all.Count.ToString();
        StatValid.Text = _all.Count(p => p.DaysLeft >= warn).ToString();
        StatSoon.Text = _all.Count(p => p.DaysLeft is >= 0 && p.DaysLeft < warn).ToString();
        StatSoonLabel.Text = $"EXPIRING IN < {warn} DAYS";
        StatExpired.Text = _all.Count(p => p.DaysLeft is null or < 0).ToString();
        ApplyFilter();
        CertList.SelectedItem = _all.FirstOrDefault(p => p.Id == selectId) ?? (CertList.Items.Count > 0 ? CertList.Items[0] : null);
        EmptyList.Visibility = _all.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ShowDetails();
    }

    private void ApplyFilter()
    {
        var q = SearchBox.Text.Trim();
        CertList.ItemsSource = string.IsNullOrEmpty(q)
            ? _all
            : _all.Where(p => p.Name.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                              p.Domains.Any(d => d.Contains(q, StringComparison.OrdinalIgnoreCase))).ToList();
    }

    private void Search_TextChanged(object sender, TextChangedEventArgs e) => ApplyFilter();

    private void CertList_SelectionChanged(object sender, SelectionChangedEventArgs e) => ShowDetails();

    private void ShowDetails()
    {
        var p = Selected;
        NoSelection.Visibility = p == null ? Visibility.Visible : Visibility.Collapsed;
        DetailsPanel.Visibility = p == null ? Visibility.Collapsed : Visibility.Visible;
        if (p == null) return;

        DName.Text = p.Name;
        DDomains.ItemsSource = p.Domains;
        DStatusText.Text = p.DaysLeft == null ? "Not issued" : p.StatusText;
        DStatusBadge.Background = (System.Windows.Media.Brush)new LevelBrushConverter { Part = "Background" }.Convert(p.StatusLevel, null!, null, null!);
        DStatusText.Foreground = (System.Windows.Media.Brush)new LevelBrushConverter().Convert(p.StatusLevel, null!, null, null!);
        RenewText.Text = p.Latest == null ? "Request now" : "Renew";

        var latest = p.Latest;
        ExportBtn.IsEnabled = InstallBtn.IsEnabled = latest != null;

        InfoGrid.Children.Clear();
        InfoGrid.RowDefinitions.Clear();
        AddInfo("Environment", p.EnvironmentDisplay + (p.Environment == AcmeEnvironment.Staging ? "  (test certificate — not trusted by browsers)" : ""));
        AddInfo("Validation", p.ChallengeDisplay + (p.Challenge == ChallengeMethod.HttpWebRoot ? $"  ·  {p.WebRootPath}" : ""));
        AddInfo("Key type", p.KeyType.ToString().Replace("Rsa", "RSA ").Replace("EcdsaP", "ECDSA P-"));
        if (!string.IsNullOrWhiteSpace(p.Email)) AddInfo("E-mail", p.Email!);
        if (latest != null)
        {
            AddInfo("Issued", latest.IssuedDisplay);
            AddInfo("Valid", latest.ValidDisplay);
            AddInfo("Issuer", latest.Issuer);
            AddInfo("Thumbprint", latest.Thumbprint, mono: true);
            AddInfo("Serial", latest.SerialNumber, mono: true);
        }
        AddInfo("Created", p.CreatedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));

        if (latest != null)
        {
            var folder = CertificateStore.IssuanceFolder(p, latest);
            var b = CertificateStore.BaseFileName(p);
            FilesList.ItemsSource = CertFileKind.All
                .SelectMany(k => k.FileNames(b).Select(name => (k, name)))
                .Where(x => File.Exists(Path.Combine(folder, x.name)))
                .Select(x => new FileRow
                {
                    Name = x.name,
                    Ext = Path.GetExtension(x.name).TrimStart('.').ToUpperInvariant(),
                    Label = x.k.Label,
                    Description = x.k.Description,
                    FullPath = Path.Combine(folder, x.name)
                }).ToList();
            var missing = !File.Exists(Path.Combine(folder, b + CertFileKind.Key));
            NoFiles.Text = "⚠ The stored files for this issuance are missing (moved or deleted outside the app). Click Renew to request new ones.";
            NoFiles.Visibility = missing ? Visibility.Visible : Visibility.Collapsed;
            ExportBtn.IsEnabled = InstallBtn.IsEnabled = !missing;
        }
        else
        {
            FilesList.ItemsSource = null;
            NoFiles.Text = "This certificate has not been issued yet — click Renew to request it.";
            NoFiles.Visibility = Visibility.Visible;
        }

        var hasPwd = p.ProtectedPfxPassword != null;
        PfxPwdHint.Text = hasPwd
            ? "The PFX is password-protected. The password is stored encrypted for your Windows account (DPAPI)."
            : "The PFX has no password.";
        ShowPwdBtn.Visibility = hasPwd ? Visibility.Visible : Visibility.Collapsed;
        HistoryList.ItemsSource = p.History.OrderByDescending(h => h.IssuedUtc).ToList();
        TargetsList.ItemsSource = null;
        TargetsList.ItemsSource = p.Targets;
        NoTargets.Visibility = p.Targets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DeployAllBtn.IsEnabled = p.Targets.Count > 0 && latest != null && !_deploying;
    }

    private void AddInfo(string label, string value, bool mono = false)
    {
        var row = InfoGrid.RowDefinitions.Count;
        InfoGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var l = new TextBlock { Text = label, Foreground = (System.Windows.Media.Brush)FindResource("TextMutedBrush"), FontSize = 12.5, Margin = new Thickness(0, 3, 0, 3) };
        var v = new TextBox
        {
            Text = value,
            FontSize = 12.5, Margin = new Thickness(0, 3, 0, 3), Style = (Style)FindResource("SelectableText")
        };
        if (mono) v.FontFamily = (System.Windows.Media.FontFamily)FindResource("MonoFont");
        Grid.SetRow(l, row);
        Grid.SetRow(v, row);
        Grid.SetColumn(v, 1);
        InfoGrid.Children.Add(l);
        InfoGrid.Children.Add(v);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Reload();

    private void New_Click(object sender, RoutedEventArgs e) => MainWindow.Instance?.ShowNewCertificate();

    private void Renew_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p) MainWindow.Instance?.ShowNewCertificate(p);
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { Latest: { } latest } p) Modal.ShowWindow(new ExportDialog(p, latest));
    }

    private void Install_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Latest: { } latest } p) return;
        var choice = Modal.Show("Install in Windows certificate store",
            "Where should the certificate and its private key be installed?\n\n" +
            "• Local Machine — used by IIS, RDP, SQL Server and Windows services (requires running CertificateGet as administrator).\n" +
            "• Current User — only for your own account.\n\nIntermediate certificates are added to the Intermediate Certification Authorities store.",
            ModalKind.Question, new[] { "Local Machine", "Current User", "Cancel" });
        if (choice is null or "Cancel") return;
        var location = choice == "Local Machine" ? StoreLocation.LocalMachine : StoreLocation.CurrentUser;
        try
        {
            CertificateStore.InstallToWindowsStore(p, latest, location);
            ActivityLog.Success("Install", $"Installed certificate {latest.Thumbprint} into {location}\\My.", p.Name);
            Modal.Success("Installed", $"The certificate was installed into {location}\\Personal.\n\nThumbprint: {latest.Thumbprint}");
        }
        catch (Exception ex)
        {
            ActivityLog.Error("Install", $"Install into {location} failed: {ex.Message}", p.Name);
            var msg = ex.Message;
            if (location == StoreLocation.LocalMachine && ex is System.Security.Cryptography.CryptographicException or UnauthorizedAccessException)
                msg += "\n\nInstalling into Local Machine requires running CertificateGet as administrator.";
            Modal.Error("Install failed", msg);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        Shell.OpenFolder(p.Latest != null ? CertificateStore.IssuanceFolder(p, p.Latest) : CertificateStore.ProfileFolder(p));
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        if (!Modal.Confirm("Delete certificate",
                $"Delete \"{p.Name}\" and ALL of its stored files ({p.History.Count} issuance(s)), including private keys?\n\n" +
                "Certificates already installed on servers keep working. This cannot be undone.", "Delete", "Cancel", danger: true))
            return;
        try
        {
            CertificateStore.Delete(p);
            ActivityLog.Warning("Store", $"Deleted certificate \"{p.Name}\" and its files.", p.Name);
        }
        catch (Exception ex)
        {
            Modal.Error("Delete failed", ex.Message);
        }
    }

    private void ShowPwd_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        var pwd = Secret.Unprotect(p.ProtectedPfxPassword);
        if (pwd == null)
        {
            Modal.Warning("Password unavailable", "The stored password could not be decrypted (it was saved by a different Windows user or machine).");
            return;
        }
        ActivityLog.Info("Store", "PFX password revealed.", p.Name);
        var r = Modal.Show("PFX password", pwd, ModalKind.Info, new[] { "Copy", "Close" });
        if (r == "Copy") Shell.CopyToClipboard(pwd);
    }

    private static T? Ctx<T>(object sender) where T : class => (sender as FrameworkElement)?.DataContext as T;

    private void FileShow_Click(object sender, RoutedEventArgs e)
    {
        if (Ctx<FileRow>(sender) is { } f) Shell.SelectFile(f.FullPath);
    }

    private void FileCopy_Click(object sender, RoutedEventArgs e)
    {
        if (Ctx<FileRow>(sender) is { } f) Shell.CopyToClipboard(f.FullPath);
    }

    private void FileBase64_Click(object sender, RoutedEventArgs e)
    {
        if (Ctx<FileRow>(sender) is not { } f) return;
        Shell.CopyToClipboard(Convert.ToBase64String(File.ReadAllBytes(f.FullPath)));
        ActivityLog.Info("Store", $"Copied {f.Name} to the clipboard as Base64.", Selected?.Name);
    }

    // ---------- deployment ----------

    private void AddTarget_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p) return;
        var dlg = new DeployTargetDialog(p, null);
        if (Modal.ShowWindow(dlg) != true || dlg.Result == null) return;
        p.Targets.Add(dlg.Result);
        CertificateStore.Save(p);
        ActivityLog.Info("Deploy", $"Added deployment target \"{dlg.Result.Name}\" ({dlg.Result.TypeDisplay}).", p.Name);
    }

    private void EditTarget_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p || Ctx<DeployTarget>(sender) is not { } t) return;
        if (Modal.ShowWindow(new DeployTargetDialog(p, t)) != true) return;
        CertificateStore.Save(p);
        ActivityLog.Info("Deploy", $"Updated deployment target \"{t.Name}\".", p.Name);
    }

    private void RemoveTarget_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { } p || Ctx<DeployTarget>(sender) is not { } t) return;
        if (!Modal.Confirm("Remove target", $"Stop deploying \"{p.Name}\" to \"{t.Name}\"?\n\nNothing is removed from the server.", "Remove", "Cancel", danger: true)) return;
        p.Targets.Remove(t);
        CertificateStore.Save(p);
        ActivityLog.Warning("Deploy", $"Removed deployment target \"{t.Name}\".", p.Name);
    }

    private async void DeployAll_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Latest: not null } p) return;
        var targets = p.Targets.Where(t => t.Enabled).ToList();
        if (targets.Count == 0) { Modal.Info("Nothing to deploy", "All targets of this certificate are disabled."); return; }
        if (!Modal.Confirm("Deploy certificate",
                $"Push the current certificate of \"{p.Name}\" (expires {p.Latest.NotAfter.ToLocalTime():yyyy-MM-dd}) to:\n\n" +
                string.Join("\n", targets.Select(t => "• " + t.Name)) + "\n\nServices on those servers may be restarted.", "Deploy", "Cancel"))
            return;
        await RunDeploy(p, targets);
    }

    private async void DeployOne_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is not { Latest: not null } p || Ctx<DeployTarget>(sender) is not { } t) return;
        if (!Modal.Confirm("Deploy certificate", $"Push the current certificate of \"{p.Name}\" to \"{t.Name}\" now?", "Deploy", "Cancel")) return;
        await RunDeploy(p, new List<DeployTarget> { t });
    }

    private async Task RunDeploy(CertificateProfile p, List<DeployTarget> targets)
    {
        _deploying = true;
        DeployAllBtn.IsEnabled = false;
        var lines = new List<string>();
        int ok = 0, failed = 0;
        try
        {
            foreach (var t in targets)
            {
                var success = await DeployService.DeployAsync(p, p.Latest!, t,
                    (level, msg) => Dispatcher.Invoke(() => lines.Add((level == LogLevel.Error ? "✗ " : level == LogLevel.Warning ? "! " : "• ") + msg)),
                    new ModalDeployUi());
                if (success) ok++; else failed++;
            }
        }
        finally
        {
            _deploying = false;
            Reload(p.Id);
        }
        Modal.Show(failed == 0 ? "Deployment finished" : "Deployment finished with errors",
            $"{ok} target(s) OK, {failed} failed.\n\n" + string.Join("\n", lines),
            failed == 0 ? ModalKind.Success : ModalKind.Warning, new[] { "Close" });
    }

    private void HistoryOpen_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p && Ctx<IssuedCertificate>(sender) is { } i) Shell.OpenFolder(CertificateStore.IssuanceFolder(p, i));
    }

    private void HistoryExport_Click(object sender, RoutedEventArgs e)
    {
        if (Selected is { } p && Ctx<IssuedCertificate>(sender) is { } i) Modal.ShowWindow(new ExportDialog(p, i));
    }
}
