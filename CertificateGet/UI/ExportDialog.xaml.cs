using System.Windows;
using System.Windows.Input;
using CertificateGet.Models;
using CertificateGet.Services;
using Microsoft.Win32;

namespace CertificateGet.UI;

public partial class ExportDialog : Window
{
    public class FormatOption
    {
        public string Suffix { get; init; } = "";
        public string Label { get; init; } = "";
        public string Description { get; init; } = "";
        public string FileName { get; init; } = "";
        public bool Selected { get; set; } = true;
    }

    private readonly CertificateProfile _profile;
    private readonly IssuedCertificate _issued;
    private readonly List<FormatOption> _formats;

    public ExportDialog(CertificateProfile profile, IssuedCertificate issued)
    {
        InitializeComponent();
        _profile = profile;
        _issued = issued;
        SubtitleText.Text = $"{profile.Name} · issued {issued.IssuedDisplay} · expires {issued.NotAfter.ToLocalTime():yyyy-MM-dd}";
        var b = CertificateStore.BaseFileName(profile);
        _formats = CertFileKind.All.Select(k => new FormatOption
        {
            Suffix = k.Suffix, Label = k.Label, Description = k.Description, FileName = b + k.Suffix
        }).ToList();
        FormatsList.ItemsSource = _formats;
        FolderBox.Text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), CertificateStore.SafeName(profile.Name));
        if (profile.ProtectedPfxPassword == null)
        {
            UseStoredPwd.Content = "Use the stored password (none)";
        }
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    private void PwdMode_Changed(object sender, RoutedEventArgs e)
    {
        if (NewPwd != null) NewPwd.IsEnabled = UseNewPwd.IsChecked == true;
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "Choose export folder" };
        if (Directory.Exists(FolderBox.Text)) dlg.InitialDirectory = FolderBox.Text;
        if (dlg.ShowDialog(this) == true) FolderBox.Text = dlg.FolderName;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var selected = _formats.Where(f => f.Selected).Select(f => f.Suffix).ToList();
        if (selected.Count == 0)
        {
            Modal.Warning("Nothing selected", "Choose at least one format to export.");
            return;
        }
        if (string.IsNullOrWhiteSpace(FolderBox.Text))
        {
            Modal.Warning("No folder", "Choose a destination folder.");
            return;
        }
        var pwd = UseNewPwd.IsChecked == true ? NewPwd.Password : Secret.Unprotect(_profile.ProtectedPfxPassword);
        try
        {
            var files = CertificateStore.Export(_profile, _issued, FolderBox.Text, selected, pwd);
            ActivityLog.Success("Export", $"Exported {files.Count} file(s) to {FolderBox.Text}: {string.Join(", ", files)}", _profile.Name);
            var r = Modal.Show("Export complete", $"{files.Count} file(s) written to:\n{FolderBox.Text}\n\n{string.Join("\n", files)}",
                ModalKind.Success, new[] { "Open folder", "Close" });
            if (r == "Open folder") Shell.OpenFolder(FolderBox.Text);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            ActivityLog.Error("Export", $"Export failed: {ex.Message}", _profile.Name);
            Modal.Error("Export failed", ex.Message);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
