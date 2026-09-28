using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CertificateGet.Models;
using CertificateGet.Services;
using CertificateGet.UI;
using Microsoft.Win32;

namespace CertificateGet.Views;

public partial class NewCertificateView : UserControl, IIssueUi
{
    public class StepItem
    {
        public string Time { get; init; } = "";
        public LogLevel Level { get; init; }
        public string Message { get; init; } = "";
    }

    private static readonly Regex HostRegex = new(@"^(\*\.)?([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z0-9-]{2,63}$", RegexOptions.Compiled);

    private readonly ObservableCollection<StepItem> _steps = new();
    private CertificateProfile? _renewing;
    private CertificateProfile? _lastIssuedProfile;
    private CancellationTokenSource? _cts;

    public bool IsBusy { get; private set; }

    public NewCertificateView()
    {
        InitializeComponent();
        StepsList.ItemsSource = _steps;
        ResetForm();
    }

    // ---------- form state ----------

    public void ResetForm()
    {
        _renewing = null;
        PageTitle.Text = "New certificate";
        PageSubtitle.Text = "Choose the domains, how the certificate authority should verify you control them, and the output options.";
        RequestText.Text = "Request certificate";
        TypeStandard.IsChecked = true;
        DomainsBox.Text = "";
        WildBaseBox.Text = "";
        WildIncludeApex.IsChecked = true;
        NameBox.Text = "";
        MHttpSelf.IsChecked = true;
        PortBox.Text = "80";
        WebRootBox.Text = "";
        var s = SettingsService.Current;
        EnvStaging.IsChecked = s.DefaultEnvironment == AcmeEnvironment.Staging;
        EnvProduction.IsChecked = s.DefaultEnvironment == AcmeEnvironment.Production;
        SelectAuthority(s.DefaultAuthority);
        SelectKeyType(s.DefaultKeyType);
        EmailBox.Text = s.DefaultEmail ?? "";
        PfxPwd.Password = PfxPwd2.Password = "";
        PwdHint.Text = "Leave empty for a PFX without password. The password is stored encrypted with your Windows account so you can re-export later.";
        UpdateMethodPanels();
    }

    public void LoadForRenewal(CertificateProfile p)
    {
        _renewing = p;
        PageTitle.Text = p.Latest == null ? $"Request: {p.Name}" : $"Renew: {p.Name}";
        PageSubtitle.Text = "Settings are loaded from the stored certificate. Adjust anything if needed — the new files are added to the same certificate's history.";
        RequestText.Text = p.Latest == null ? "Request certificate" : "Renew certificate";

        var wildcard = p.Domains.Count is 1 or 2 && p.Domains[0].StartsWith("*.") &&
                       (p.Domains.Count == 1 || p.Domains[1] == p.Domains[0][2..]);
        if (wildcard)
        {
            TypeWildcard.IsChecked = true;
            WildBaseBox.Text = p.Domains[0][2..];
            WildIncludeApex.IsChecked = p.Domains.Count == 2;
        }
        else
        {
            TypeStandard.IsChecked = true;
            DomainsBox.Text = string.Join(Environment.NewLine, p.Domains);
        }
        NameBox.Text = p.Name;
        MHttpSelf.IsChecked = p.Challenge == ChallengeMethod.HttpSelfHosted;
        MHttpWebRoot.IsChecked = p.Challenge == ChallengeMethod.HttpWebRoot;
        MDnsManual.IsChecked = p.Challenge == ChallengeMethod.DnsManual;
        MDnsCloudflare.IsChecked = p.Challenge == ChallengeMethod.DnsCloudflare;
        MDnsHostinger.IsChecked = p.Challenge == ChallengeMethod.DnsHostinger;
        MDnsConstellix.IsChecked = p.Challenge == ChallengeMethod.DnsConstellix;
        MDnsAcmeDns.IsChecked = p.Challenge == ChallengeMethod.DnsAcmeDns;
        MDnsMadeEasy.IsChecked = p.Challenge == ChallengeMethod.DnsMadeEasy;
        MDnsNamecheap.IsChecked = p.Challenge == ChallengeMethod.DnsNamecheap;
        PortBox.Text = p.HttpPort.ToString();
        WebRootBox.Text = p.WebRootPath ?? "";
        EnvStaging.IsChecked = p.Environment == AcmeEnvironment.Staging;
        EnvProduction.IsChecked = p.Environment == AcmeEnvironment.Production;
        SelectAuthority(p.Authority);
        SelectKeyType(p.KeyType);
        EmailBox.Text = p.Email ?? "";
        var pwd = Secret.Unprotect(p.ProtectedPfxPassword) ?? "";
        PfxPwd.Password = PfxPwd2.Password = pwd;
        PwdHint.Text = p.ProtectedPfxPassword == null
            ? "No PFX password was used before. Enter one now if you want the new PFX protected."
            : "The previous PFX password has been filled in. Change it here if you want a different one.";
        UpdateMethodPanels();
    }

    private void SelectAuthority(CertificateAuthority ca)
    {
        (ca == CertificateAuthority.ZeroSsl ? CaZeroSsl : CaLetsEncrypt).IsChecked = true;
        Ca_Changed(this, null!);
    }

    private void Ca_Changed(object sender, RoutedEventArgs e)
    {
        if (CaHint == null || EnvPanel == null) return; // during InitializeComponent
        var zero = CaZeroSsl.IsChecked == true;
        // ZeroSSL has no staging server: every request is a trusted certificate.
        EnvPanel.Visibility = zero ? Visibility.Collapsed : Visibility.Visible;
        CaHint.Text = zero
            ? (SettingsService.Current.ProtectedZeroSslApiKey != null
                ? "Trusted 90-day certificates, no rate limits. The ACME account is linked to your ZeroSSL account through the API key in Settings."
                : "Trusted 90-day certificates, no rate limits. The first request links a ZeroSSL account to the contact e-mail below (created if it does not exist); or enter a ZeroSSL API key in Settings.")
            : "Free, trusted 90-day certificates. The default.";
    }

    private void SelectKeyType(CertKeyType type)
    {
        foreach (ComboBoxItem item in KeyTypeBox.Items)
            if ((string)item.Tag == type.ToString()) KeyTypeBox.SelectedItem = item;
        KeyTypeBox.SelectedIndex = Math.Max(0, KeyTypeBox.SelectedIndex);
    }

    private void Type_Changed(object sender, RoutedEventArgs e)
    {
        if (StandardPanel == null) return;
        var wild = TypeWildcard.IsChecked == true;
        StandardPanel.Visibility = wild ? Visibility.Collapsed : Visibility.Visible;
        WildcardPanel.Visibility = wild ? Visibility.Visible : Visibility.Collapsed;
        UpdateMethodPanels();
    }

    private void Domains_TextChanged(object sender, TextChangedEventArgs e) => Domains_Changed(sender, e);

    private void Domains_Changed(object sender, RoutedEventArgs e)
    {
        if (ApexRun == null) return;
        var b = NormalizeHost(WildBaseBox.Text);
        ApexRun.Text = $"({(string.IsNullOrEmpty(b) ? "example.com" : b)})";
        UpdateMethodPanels();
    }

    private void Method_Changed(object sender, RoutedEventArgs e) => UpdateMethodPanels();

    private void UpdateMethodPanels()
    {
        if (HttpPortPanel == null || MHttpSelf == null || NamecheapHint == null) return;
        var needsDns = TypeWildcard.IsChecked == true || DomainsBox.Text.Contains('*');
        MHttpSelf.IsEnabled = MHttpWebRoot.IsEnabled = !needsDns;
        if (needsDns && MHttpSelf.IsChecked != true && MHttpWebRoot.IsChecked != true) { /* already DNS */ }
        else if (needsDns) MDnsManual.IsChecked = true;

        HttpPortPanel.Visibility = MHttpSelf.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        WebRootPanel.Visibility = MHttpWebRoot.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        var hasToken = SettingsService.Current.ProtectedCloudflareToken != null;
        CloudflareHint.Text = hasToken
            ? "Creates and removes the TXT records through the Cloudflare API using the token saved in Settings."
            : "Creates and removes the TXT records through the Cloudflare API. ⚠ No API token configured yet — add one in Settings.";
        HostingerHint.Text = SettingsService.Current.ProtectedHostingerToken != null
            ? "Creates and removes the TXT records through the Hostinger API using the token saved in Settings."
            : "Creates and removes the TXT records through the Hostinger API. ⚠ No API token configured yet — add one in Settings.";
        ConstellixHint.Text = SettingsService.Current.ProtectedConstellixApiKey != null && SettingsService.Current.ProtectedConstellixSecretKey != null
            ? "Creates and removes the TXT records through the Constellix API using the keys saved in Settings."
            : "Creates and removes the TXT records through the Constellix API. ⚠ No API key / secret key configured yet — add them in Settings.";
        var s = SettingsService.Current;
        var registered = s.AcmeDnsAccounts.Select(a => a.Domain).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var newDomains = CollectDomains().Select(d => d.StartsWith("*.") ? d[2..] : d).Distinct().Where(d => !registered.Contains(d)).ToList();
        AcmeDnsHint.Text = "Works with any DNS host. Once per domain you create a CNAME for _acme-challenge; after that the app only talks to the acme-dns server" +
                           $" ({AcmeDnsProvider.NormalizeServer(s.AcmeDnsServer)})." +
                           (newDomains.Count > 0 ? $" Not registered yet: {string.Join(", ", newDomains.Take(4))}{(newDomains.Count > 4 ? "…" : "")} — the app registers them and shows the CNAME to create." : "");
        DnsMadeEasyHint.Text = s.ProtectedDnsMadeEasyApiKey != null && s.ProtectedDnsMadeEasySecretKey != null
            ? "Creates and removes the TXT records through the DNS Made Easy API using the keys saved in Settings."
            : "Creates and removes the TXT records through the DNS Made Easy API. ⚠ No API key / secret key configured yet — add them in Settings.";
        NamecheapHint.Text = !string.IsNullOrWhiteSpace(s.NamecheapApiUser) && s.ProtectedNamecheapApiKey != null
            ? "Creates and removes the TXT records through the Namecheap API using the key saved in Settings. Other records are written back unchanged."
            : "Creates and removes the TXT records through the Namecheap API. ⚠ No API user / key configured yet — add them in Settings.";
    }

    private void BrowseWebRoot_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Select the web site's root folder" };
        if (Directory.Exists(WebRootBox.Text)) dlg.InitialDirectory = WebRootBox.Text;
        if (dlg.ShowDialog() == true) WebRootBox.Text = dlg.FolderName;
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetForm();

    private static string NormalizeHost(string raw)
    {
        var h = raw.Trim().TrimEnd('.').ToLowerInvariant();
        if (h.StartsWith("http://")) h = h[7..];
        if (h.StartsWith("https://")) h = h[8..];
        h = h.Split('/')[0];
        if (h.Length == 0) return h;
        try
        {
            var prefix = h.StartsWith("*.") ? "*." : "";
            return prefix + new IdnMapping().GetAscii(h[prefix.Length..]);
        }
        catch
        {
            return h;
        }
    }

    private List<string> CollectDomains()
    {
        if (TypeWildcard.IsChecked == true)
        {
            var b = NormalizeHost(WildBaseBox.Text);
            if (b.StartsWith("*.")) b = b[2..];
            if (string.IsNullOrEmpty(b)) return new();
            var list = new List<string> { "*." + b };
            if (WildIncludeApex.IsChecked == true) list.Add(b);
            return list;
        }
        return DomainsBox.Text
            .Split(new[] { '\r', '\n', ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeHost)
            .Where(d => d.Length > 0)
            .Distinct()
            .ToList();
    }

    private CertificateProfile? BuildProfile()
    {
        var domains = CollectDomains();
        if (domains.Count == 0)
        {
            Modal.Warning("Domains required", "Enter at least one domain name.");
            return null;
        }
        var bad = domains.Where(d => !HostRegex.IsMatch(d)).ToList();
        if (bad.Count > 0)
        {
            Modal.Warning("Invalid domain name", "These names are not valid host names:\n\n" + string.Join("\n", bad));
            return null;
        }
        if (domains.Count > 100)
        {
            Modal.Warning("Too many names", "A certificate can have at most 100 names.");
            return null;
        }

        var method = MHttpSelf.IsChecked == true ? ChallengeMethod.HttpSelfHosted
            : MHttpWebRoot.IsChecked == true ? ChallengeMethod.HttpWebRoot
            : MDnsCloudflare.IsChecked == true ? ChallengeMethod.DnsCloudflare
            : MDnsHostinger.IsChecked == true ? ChallengeMethod.DnsHostinger
            : MDnsConstellix.IsChecked == true ? ChallengeMethod.DnsConstellix
            : MDnsAcmeDns.IsChecked == true ? ChallengeMethod.DnsAcmeDns
            : MDnsMadeEasy.IsChecked == true ? ChallengeMethod.DnsMadeEasy
            : MDnsNamecheap.IsChecked == true ? ChallengeMethod.DnsNamecheap
            : ChallengeMethod.DnsManual;

        if (domains.Any(d => d.StartsWith("*.")) && method is ChallengeMethod.HttpSelfHosted or ChallengeMethod.HttpWebRoot)
        {
            Modal.Warning("DNS validation required", "Wildcard names can only be validated with DNS. Choose a DNS method.");
            return null;
        }
        if (!int.TryParse(PortBox.Text, out var port) || port is < 1 or > 65535)
        {
            Modal.Warning("Invalid port", "Enter a port number between 1 and 65535.");
            return null;
        }
        if (method == ChallengeMethod.HttpWebRoot && !Directory.Exists(WebRootBox.Text))
        {
            Modal.Warning("Web root not found", "Choose the existing root folder of the web site that serves these domains.");
            return null;
        }
        if (method == ChallengeMethod.DnsCloudflare && SettingsService.Current.ProtectedCloudflareToken == null)
        {
            Modal.Warning("Cloudflare token missing", "Add your Cloudflare API token in Settings (it needs Zone:Read and DNS:Edit permissions).");
            return null;
        }
        if (method == ChallengeMethod.DnsConstellix &&
            (SettingsService.Current.ProtectedConstellixApiKey == null || SettingsService.Current.ProtectedConstellixSecretKey == null))
        {
            Modal.Warning("Constellix keys missing", "Add your Constellix API key and secret key in Settings (Constellix → Edit My Account → API Keys).");
            return null;
        }
        if (method == ChallengeMethod.DnsMadeEasy &&
            (SettingsService.Current.ProtectedDnsMadeEasyApiKey == null || SettingsService.Current.ProtectedDnsMadeEasySecretKey == null))
        {
            Modal.Warning("DNS Made Easy keys missing", "Add your DNS Made Easy API key and secret key in Settings (DNS Made Easy → Config → Account Information).");
            return null;
        }
        if (method == ChallengeMethod.DnsNamecheap &&
            (string.IsNullOrWhiteSpace(SettingsService.Current.NamecheapApiUser) || SettingsService.Current.ProtectedNamecheapApiKey == null))
        {
            Modal.Warning("Namecheap API access missing", "Add your Namecheap API user and API key in Settings (Namecheap → Profile → Tools → API Access).");
            return null;
        }
        if (method == ChallengeMethod.DnsHostinger && SettingsService.Current.ProtectedHostingerToken == null)
        {
            Modal.Warning("Hostinger token missing", "Add your Hostinger API token in Settings (hPanel → Account → API).");
            return null;
        }
        if (PfxPwd.Password != PfxPwd2.Password)
        {
            Modal.Warning("Passwords differ", "The PFX password and its confirmation do not match.");
            return null;
        }
        var email = EmailBox.Text.Trim();
        if (email.Length > 0 && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            Modal.Warning("Invalid e-mail", "Enter a valid e-mail address or leave it empty.");
            return null;
        }

        var p = _renewing ?? new CertificateProfile();
        p.Domains = domains;
        p.Name = string.IsNullOrWhiteSpace(NameBox.Text) ? domains[0] : NameBox.Text.Trim();
        p.Challenge = method;
        p.HttpPort = port;
        p.WebRootPath = method == ChallengeMethod.HttpWebRoot ? WebRootBox.Text.Trim() : p.WebRootPath;
        p.Authority = CaZeroSsl.IsChecked == true ? CertificateAuthority.ZeroSsl : CertificateAuthority.LetsEncrypt;
        p.Environment = p.Authority == CertificateAuthority.ZeroSsl || EnvProduction.IsChecked == true
            ? AcmeEnvironment.Production : AcmeEnvironment.Staging;
        p.KeyType = Enum.Parse<CertKeyType>((string)((ComboBoxItem)KeyTypeBox.SelectedItem).Tag);
        p.Email = email.Length > 0 ? email : null;
        p.ProtectedPfxPassword = Secret.Protect(PfxPwd.Password);
        return p;
    }

    // ---------- run ----------

    private async void Request_Click(object sender, RoutedEventArgs e)
    {
        var p = BuildProfile();
        if (p == null) return;

        if (p.Authority == CertificateAuthority.ZeroSsl && string.IsNullOrWhiteSpace(p.Email ?? SettingsService.Current.DefaultEmail) &&
            SettingsService.Current.ProtectedZeroSslApiKey == null &&
            !File.Exists(Path.Combine(SettingsService.AccountsPath, AcmeService.AccountFileName(p))))
        {
            Modal.Warning("E-mail needed for ZeroSSL",
                "ZeroSSL links every ACME account to a ZeroSSL account. Enter a contact e-mail (the ZeroSSL account is created for it if needed), or add a ZeroSSL API key in Settings.");
            return;
        }

        if (p.Authority == CertificateAuthority.LetsEncrypt && p.Environment == AcmeEnvironment.Production && p.History.Count == 0 &&
            !Modal.Confirm("Request a production certificate?",
                $"This will request a trusted certificate for:\n\n{string.Join("\n", p.Domains)}\n\n" +
                "Production has strict rate limits (e.g. 5 failed validations per hour per account). If you are unsure your setup works, try Staging first.",
                "Request", "Cancel"))
            return;

        var pwd = PfxPwd.Password.Length > 0 ? PfxPwd.Password : null;
        CertificateStore.Save(p); // keep the definition even if the request fails, so it can be retried
        _renewing = p;

        SetBusy(true);
        _steps.Clear();
        _lastIssuedProfile = null;
        SummaryText.Text = $"{p.DomainsDisplay}\n{(p.Authority == CertificateAuthority.ZeroSsl ? "ZeroSSL" : "Let's Encrypt " + p.EnvironmentDisplay)} · {p.ChallengeDisplay} · {p.KeyType}";
        _cts = new CancellationTokenSource();
        try
        {
            var service = new AcmeService(p, (level, msg) => Dispatcher.Invoke(() => AddStep(level, msg)));
            var issued = await Task.Run(() => service.IssueAsync(pwd, this, _cts.Token));
            _lastIssuedProfile = p;
            var deployText = "";
            var autoTargets = p.Targets.Count(t => t.Enabled && t.AutoDeploy);
            if (autoTargets > 0)
            {
                AddStep(LogLevel.Info, $"Deploying to {autoTargets} target(s)…");
                var (ok, failed) = await DeployService.DeployAllAsync(p, automaticOnly: true,
                    (level, msg) => Dispatcher.Invoke(() => AddStep(level, msg)), new ModalDeployUi());
                deployText = $"\n\nDeployment: {ok} target(s) OK" + (failed > 0 ? $", {failed} FAILED — see the progress log." : ".");
            }
            SetState(deployText.Contains("FAILED") ? "Issued, deploy failed" : "Issued", deployText.Contains("FAILED") ? "warn" : "ok");
            ViewCertBtn.Visibility = Visibility.Visible;
            var r = Modal.Show("Certificate issued",
                $"{p.Name}\nValid until {issued.NotAfter.ToLocalTime():yyyy-MM-dd HH:mm}\n\nFiles saved:\n{string.Join("\n", issued.Files)}{deployText}",
                deployText.Contains("FAILED") ? ModalKind.Warning : ModalKind.Success, new[] { "View certificate", "Open folder", "Close" });
            if (r == "View certificate") MainWindow.Instance?.ShowCertificates(p.Id);
            else if (r == "Open folder") Shell.OpenFolder(CertificateStore.IssuanceFolder(p, issued));
        }
        catch (OperationCanceledException)
        {
            AddStep(LogLevel.Warning, "Request cancelled.");
            ActivityLog.Warning("Issue", "Request cancelled by user.", p.Name);
            SetState("Cancelled", "warn");
        }
        catch (Exception ex)
        {
            var msg = AcmeService.Describe(ex);
            AddStep(LogLevel.Error, msg);
            ActivityLog.Error("Issue", "Request failed: " + msg, p.Name);
            SetState("Failed", "bad");
            Modal.Error("Certificate request failed", msg);
        }
        finally
        {
            SetBusy(false);
            _cts?.Dispose();
            _cts = null;
            CertificateStore.RaiseChanged();
        }
    }

    private void SetBusy(bool busy)
    {
        IsBusy = busy;
        FormPanel.IsEnabled = !busy;
        CancelBtn.IsEnabled = busy;
        Busy.Visibility = busy ? Visibility.Visible : Visibility.Hidden;
        if (busy)
        {
            ViewCertBtn.Visibility = Visibility.Collapsed;
            SetState("Running", "info");
        }
    }

    private void SetState(string text, string level)
    {
        StateText.Text = text;
        StateText.Foreground = (Brush)new LevelBrushConverter().Convert(level, null!, null, null!);
        StateBadge.Background = (Brush)new LevelBrushConverter { Part = "Background" }.Convert(level, null!, null, null!);
    }

    private void AddStep(LogLevel level, string message)
    {
        _steps.Add(new StepItem { Time = DateTime.Now.ToString("HH:mm:ss"), Level = level, Message = message });
        StepsScroll.ScrollToEnd();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void ViewCert_Click(object sender, RoutedEventArgs e)
    {
        if (_lastIssuedProfile != null) MainWindow.Instance?.ShowCertificates(_lastIssuedProfile.Id);
    }

    // ---------- IIssueUi ----------

    public Task<bool> ConfirmDnsRecordsAsync(IList<DnsTxtRecord> records) =>
        Dispatcher.InvokeAsync(() => Modal.ShowWindow(new DnsRecordsDialog(records)) == true).Task;
}
