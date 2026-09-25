using System.Windows;
using CertificateGet.Models;
using CertificateGet.Services;
using CertificateGet.UI;
using CertificateGet.Views;

namespace CertificateGet;

public partial class MainWindow : Window
{
    private readonly CertificatesView _certs = new();
    private readonly NewCertificateView _new = new();
    private readonly ActivityLogView _log = new();
    private readonly SettingsView _settings = new();
    private int _dimCount;

    public static MainWindow? Instance { get; private set; }

    public MainWindow()
    {
        InitializeComponent();
        Instance = this;
        UpdateStorePath();
        NavCerts.IsChecked = true;
    }

    public void UpdateStorePath() => StorePathText.Text = SettingsService.StorePath;

    public void SetDimmed(bool on)
    {
        _dimCount = Math.Max(0, _dimCount + (on ? 1 : -1));
        DimLayer.Visibility = _dimCount > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (sender == NavCerts) { _certs.Reload(); PageHost.Content = _certs; }
        else if (sender == NavNew) PageHost.Content = _new;
        else if (sender == NavLog) { _log.Reload(); PageHost.Content = _log; }
        else if (sender == NavSettings) { _settings.Load(); PageHost.Content = _settings; }
    }

    public void ShowCertificates(string? selectId = null)
    {
        NavCerts.IsChecked = true;
        _certs.Reload(selectId);
    }

    public void ShowNewCertificate(CertificateProfile? renew = null)
    {
        if (_new.IsBusy)
        {
            Modal.Warning("Request in progress", "A certificate request is already running. Wait for it to finish first.");
            NavNew.IsChecked = true;
            return;
        }
        if (renew != null) _new.LoadForRenewal(renew); else _new.ResetForm();
        NavNew.IsChecked = true;
        PageHost.Content = _new;
    }

    private void OpenStore_Click(object sender, RoutedEventArgs e) => Shell.OpenFolder(SettingsService.StorePath);

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (_new.IsBusy && !Modal.Confirm("Request in progress",
                "A certificate request is still running. Closing now will abandon it.\n\nClose anyway?", "Close", "Stay", danger: true))
        {
            e.Cancel = true;
            return;
        }
        base.OnClosing(e);
    }
}
