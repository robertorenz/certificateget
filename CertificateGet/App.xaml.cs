using System.Windows;
using System.Windows.Threading;
using CertificateGet.Services;
using CertificateGet.UI;

namespace CertificateGet;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        SettingsService.EnsureStore();
        DispatcherUnhandledException += OnUnhandled;
        ActivityLog.Info("App", "CertificateGet started.");
    }

    private void OnUnhandled(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        ActivityLog.Error("App", "Unexpected error: " + e.Exception.Message);
        Modal.Error("Unexpected error", e.Exception.Message);
    }
}
