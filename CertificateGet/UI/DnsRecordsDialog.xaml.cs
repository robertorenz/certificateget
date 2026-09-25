using System.Windows;
using System.Windows.Input;
using CertificateGet.Models;
using CertificateGet.Services;

namespace CertificateGet.UI;

public partial class DnsRecordsDialog : Window
{
    private readonly IList<DnsTxtRecord> _records;

    public DnsRecordsDialog(IList<DnsTxtRecord> records)
    {
        InitializeComponent();
        _records = records;
        RecordsList.ItemsSource = records;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    private static DnsTxtRecord? RecordOf(object sender) => (sender as FrameworkElement)?.DataContext as DnsTxtRecord;

    private void CopyName_Click(object sender, RoutedEventArgs e)
    {
        if (RecordOf(sender) is { } r) Shell.CopyToClipboard(r.RecordName);
    }

    private void CopyValue_Click(object sender, RoutedEventArgs e)
    {
        if (RecordOf(sender) is { } r) Shell.CopyToClipboard(r.Value);
    }

    private async void Check_Click(object sender, RoutedEventArgs e)
    {
        CheckButton.IsEnabled = false;
        CheckStatus.Text = "Querying " + SettingsService.Current.DnsResolvers + "…";
        var all = await DnsChecker.AllVisibleAsync(_records);
        RecordsList.ItemsSource = null;
        RecordsList.ItemsSource = _records;
        var found = _records.Count(r => r.Found);
        CheckStatus.Text = all ? "All records are visible — ready to validate." : $"{found} of {_records.Count} visible. Propagation can take a few minutes.";
        CheckStatus.Foreground = (System.Windows.Media.Brush)FindResource(all ? "SuccessBrush" : "WarningBrush");
        CheckButton.IsEnabled = true;
    }

    private async void Continue_Click(object sender, RoutedEventArgs e)
    {
        var all = await DnsChecker.AllVisibleAsync(_records);
        if (!all)
        {
            var go = Modal.Confirm("Records not visible yet",
                "Not every TXT record is visible on the public resolvers yet. If Let's Encrypt can't see them the validation will fail " +
                "and you will have to start again.\n\nValidate anyway?", "Validate anyway", "Keep waiting");
            if (!go)
            {
                RecordsList.ItemsSource = null;
                RecordsList.ItemsSource = _records;
                return;
            }
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
