using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using CertificateGet.Models;
using CertificateGet.Services;
using Microsoft.Win32;

namespace CertificateGet.UI;

/// <summary>Asks the user (in a modal) to trust a server's key the first time we connect.</summary>
public class ModalDeployUi : IDeployUi
{
    public bool ConfirmFingerprint(string targetName, string what, string fingerprint) =>
        Application.Current.Dispatcher.Invoke(() => Modal.Confirm("Trust this server?",
            $"First connection to \"{targetName}\".\n\n{what}:\n{fingerprint}\n\n" +
            "Compare it with the value on the server — for the agent run \"CertificateGet.Agent info\"; for SSH run " +
            "\"ssh-keygen -lf /etc/ssh/ssh_host_ed25519_key.pub\". Trust and remember it?",
            "Trust", "Cancel"));
}

public partial class DeployTargetDialog : Window
{
    public class SourceOption
    {
        public string Alias { get; init; } = "";
        public string Label { get; init; } = "";
        public override string ToString() => Label;
    }

    public class FileRow
    {
        public static readonly List<SourceOption> AllOptions =
            DeployService.Sources.Select(s => new SourceOption { Alias = s.Alias, Label = s.Label }).ToList();
        public List<SourceOption> Options => AllOptions;
        public SourceOption Selected { get; set; } = AllOptions[0];
        public string RemoteName { get; set; } = "{domain}.pem";
    }

    public record SlotChip(string Name, string Count, string Tip);

    private const string Unchanged = "••••••••";
    private readonly DeployTarget _original;
    private readonly DeployTarget _work;
    private readonly ObservableCollection<FileRow> _files = new();

    public DeployTargetDialog(CertificateProfile profile, DeployTarget? existing)
    {
        InitializeComponent();
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed && e.OriginalSource is not System.Windows.Controls.TextBox) DragMove(); };
        _original = existing ?? new DeployTarget();
        _work = JsonSerializer.Deserialize<DeployTarget>(JsonSerializer.Serialize(_original, Json.Options), Json.Options)!;
        TitleText.Text = existing == null ? "Add deployment target" : "Edit deployment target";
        SubtitleText.Text = profile.Name;
        FilesList.ItemsSource = _files;
        LoadFields();
        UpdatePanels();
    }

    private void LoadFields()
    {
        var t = _work;
        NameBox.Text = t.Name;
        TypeAgent.IsChecked = t.Type == DeployType.Agent || string.IsNullOrEmpty(t.Name);
        TypeSftp.IsChecked = t.Type == DeployType.Sftp && !string.IsNullOrEmpty(t.Name);
        AgentUrlBox.Text = t.AgentUrl;
        ApiKeyBox.Password = t.ProtectedApiKey != null ? Unchanged : "";
        SlotBox.Text = t.AgentSlot;
        HostBox.Text = t.Host;
        PortBox.Text = t.Port.ToString();
        UserBox.Text = t.Username;
        AuthKey.IsChecked = t.UseKeyAuth;
        AuthPassword.IsChecked = !t.UseKeyAuth;
        SshPasswordBox.Password = t.ProtectedPassword != null ? Unchanged : "";
        KeyPathBox.Text = t.PrivateKeyPath ?? "";
        KeyPassBox.Password = t.ProtectedKeyPassphrase != null ? Unchanged : "";
        RemoteFolderBox.Text = t.RemoteFolder;
        PostCommandBox.Text = t.PostCommand ?? "";
        foreach (var f in t.Files)
            _files.Add(new FileRow { Selected = FileRow.AllOptions.FirstOrDefault(o => o.Alias == f.Source) ?? FileRow.AllOptions[0], RemoteName = f.RemoteName });
        EnabledBox.IsChecked = t.Enabled;
        AutoBox.IsChecked = t.AutoDeploy;
        UpdateFingerprints();
        UpdatePanels();
    }

    private void UpdateFingerprints()
    {
        AgentFpText.Text = string.IsNullOrEmpty(_work.AgentFingerprint)
            ? "Agent TLS fingerprint: not yet trusted — you will be asked on first connect."
            : $"Trusted agent fingerprint: {_work.AgentFingerprint[..16]}…";
        ForgetAgentFp.Visibility = string.IsNullOrEmpty(_work.AgentFingerprint) ? Visibility.Collapsed : Visibility.Visible;
        HostFpText.Text = string.IsNullOrEmpty(_work.HostKeyFingerprint)
            ? "Host key: not yet trusted — you will be asked on first connect."
            : $"Trusted host key: {_work.HostKeyFingerprint}";
        ForgetHostFp.Visibility = string.IsNullOrEmpty(_work.HostKeyFingerprint) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdatePanels()
    {
        if (!IsInitialized || AgentPanel == null || SftpPanel == null || KeyPanel == null || SshPasswordBox == null) return;
        var agent = TypeAgent.IsChecked == true;
        AgentPanel.Visibility = agent ? Visibility.Visible : Visibility.Collapsed;
        SftpPanel.Visibility = agent ? Visibility.Collapsed : Visibility.Visible;
        KeyPanel.Visibility = AuthKey.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SshPasswordBox.Visibility = AuthKey.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Type_Changed(object sender, RoutedEventArgs e) => UpdatePanels();
    private void Auth_Changed(object sender, RoutedEventArgs e) => UpdatePanels();

    private string? Secretize(string entered, string? existing) =>
        entered == Unchanged ? existing : Secret.Protect(entered);

    /// <summary>Copies the form into the working copy; returns an error message or null.</summary>
    private string? Apply()
    {
        var t = _work;
        t.Name = NameBox.Text.Trim();
        t.Type = TypeAgent.IsChecked == true ? DeployType.Agent : DeployType.Sftp;
        t.AgentUrl = AgentUrlBox.Text.Trim();
        t.ProtectedApiKey = Secretize(ApiKeyBox.Password, t.ProtectedApiKey);
        t.AgentSlot = SlotBox.Text.Trim();
        t.Host = HostBox.Text.Trim();
        t.Port = int.TryParse(PortBox.Text, out var port) ? port : 0;
        t.Username = UserBox.Text.Trim();
        t.UseKeyAuth = AuthKey.IsChecked == true;
        t.ProtectedPassword = Secretize(SshPasswordBox.Password, t.ProtectedPassword);
        t.PrivateKeyPath = KeyPathBox.Text.Trim();
        t.ProtectedKeyPassphrase = Secretize(KeyPassBox.Password, t.ProtectedKeyPassphrase);
        t.RemoteFolder = RemoteFolderBox.Text.Trim();
        t.PostCommand = string.IsNullOrWhiteSpace(PostCommandBox.Text) ? null : PostCommandBox.Text.Trim();
        t.Files = _files.Select(f => new DeployFile { Source = f.Selected.Alias, RemoteName = f.RemoteName.Trim() }).ToList();
        t.Enabled = EnabledBox.IsChecked == true;
        t.AutoDeploy = AutoBox.IsChecked == true;

        if (t.Type == DeployType.Agent)
        {
            if (!Uri.TryCreate(t.AgentUrl, UriKind.Absolute, out var u) || u.Scheme != "https")
                return "Enter the agent URL, e.g. https://server-name:9443";
            if (t.ProtectedApiKey == null) return "Enter the agent's API key (shown when the agent was installed).";
            if (t.AgentSlot.Length == 0) return "Enter the slot name configured on the agent.";
        }
        else
        {
            if (t.Host.Length == 0 || t.Username.Length == 0) return "Enter the SSH host and user.";
            if (t.Port is < 1 or > 65535) return "Enter a valid SSH port.";
            if (t.UseKeyAuth && !File.Exists(t.PrivateKeyPath)) return "The private key file was not found.";
            if (!t.RemoteFolder.StartsWith('/')) return "Enter the remote folder as an absolute path, e.g. /etc/haproxy/certs";
            if (t.Files.Count == 0) return "Add at least one file to upload.";
            if (t.Files.Any(f => f.RemoteName.Length == 0 || f.RemoteName.Contains('/'))) return "Every file needs a remote name (without folders).";
        }
        return null;
    }

    private async void TestAgent_Click(object sender, RoutedEventArgs e)
    {
        var err = Apply();
        if (err != null && !err.Contains("slot")) { Modal.Warning("Check the fields", err); return; }
        try
        {
            IsEnabled = false;
            var slots = await DeployService.GetAgentSlotsAsync(_work, new ModalDeployUi());
            SlotChips.ItemsSource = slots.Select(s => new SlotChip(s.Name, $"({s.Destinations})", s.Description)).ToList();
            UpdateFingerprints();
            Modal.Success("Agent reachable", slots.Count == 0
                ? "Connected, but no slots are configured in the agent's agent.json yet."
                : "Connected. Slots on this agent:\n\n" + string.Join("\n", slots.Select(s => $"• {s.Name} — {s.Destinations} destination(s){(s.Description.Length > 0 ? " — " + s.Description : "")}")) +
                  "\n\nClick a slot below the field to use it.");
        }
        catch (Exception ex)
        {
            UpdateFingerprints();
            Modal.Error("Agent test failed", ex.Message);
        }
        finally { IsEnabled = true; }
    }

    private void SlotChip_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is SlotChip chip) SlotBox.Text = chip.Name;
    }

    private async void TestSftp_Click(object sender, RoutedEventArgs e)
    {
        var err = Apply();
        if (err != null && !err.Contains("file")) { Modal.Warning("Check the fields", err); return; }
        try
        {
            IsEnabled = false;
            var msg = await DeployService.TestSftpAsync(_work, new ModalDeployUi());
            UpdateFingerprints();
            Modal.Info("Connection test", msg);
        }
        catch (Exception ex)
        {
            UpdateFingerprints();
            Modal.Error("Connection failed", ex.Message);
        }
        finally { IsEnabled = true; }
    }

    private void ForgetAgentFp_Click(object sender, RoutedEventArgs e) { _work.AgentFingerprint = null; UpdateFingerprints(); }
    private void ForgetHostFp_Click(object sender, RoutedEventArgs e) { _work.HostKeyFingerprint = null; UpdateFingerprints(); }

    private void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = "SSH private key (OpenSSH or PuTTY format)", Filter = "All files|*.*" };
        var ssh = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");
        if (Directory.Exists(ssh)) dlg.InitialDirectory = ssh;
        if (dlg.ShowDialog(this) == true) KeyPathBox.Text = dlg.FileName;
    }

    private void AddFile_Click(object sender, RoutedEventArgs e) => _files.Add(new FileRow());

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is FileRow row) _files.Remove(row);
    }

    private void SetFiles(params (string Alias, string Name)[] files)
    {
        _files.Clear();
        foreach (var (alias, name) in files)
            _files.Add(new FileRow { Selected = FileRow.AllOptions.First(o => o.Alias == alias), RemoteName = name });
    }

    private void PresetHaproxy_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RemoteFolderBox.Text)) RemoteFolderBox.Text = "/etc/haproxy/certs";
        SetFiles(("combined", "{domain}.pem"));
        PostCommandBox.Text = "haproxy -c -f /etc/haproxy/haproxy.cfg && systemctl reload haproxy";
    }

    private void PresetNginx_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(RemoteFolderBox.Text)) RemoteFolderBox.Text = "/etc/nginx/ssl";
        SetFiles(("fullchain", "{domain}.crt"), ("key", "{domain}.key"));
        PostCommandBox.Text = "nginx -t && systemctl reload nginx";
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var err = Apply();
        if (err != null) { Modal.Warning("Check the fields", err); return; }
        if (_work.Name.Length == 0) _work.Name = _work.Type == DeployType.Agent ? new Uri(_work.AgentUrl).Host : _work.Host;
        // copy back into the original instance
        var json = JsonSerializer.Serialize(_work, Json.Options);
        var copy = JsonSerializer.Deserialize<DeployTarget>(json, Json.Options)!;
        foreach (var prop in typeof(DeployTarget).GetProperties().Where(p => p.CanWrite))
            prop.SetValue(_original, prop.GetValue(copy));
        Result = _original;
        DialogResult = true;
    }

    public DeployTarget? Result { get; private set; }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
