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
        if (records.Count > 0 && records.All(r => r.Type == "CNAME"))
        {
            TitleText.Text = "Create these CNAME records (one time)";
            SubtitleText.Text = "They hand the _acme-challenge names to acme-dns. After this, renewals need no DNS changes.";
            WarningText.Text = "Create each record as a CNAME at the DNS provider of that domain. Remove any existing _acme-challenge TXT " +
                               "record with the same name first, because a name that has a CNAME cannot have other records.";
        }
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
        CheckStatus.Text = all ? "All records are visible — ready to continue." : $"{found} of {_records.Count} visible. Propagation can take a few minutes.";
        CheckStatus.Foreground = (System.Windows.Media.Brush)FindResource(all ? "SuccessBrush" : "WarningBrush");
        CheckButton.IsEnabled = true;
    }

    private async void Continue_Click(object sender, RoutedEventArgs e)
    {
        var all = await DnsChecker.AllVisibleAsync(_records);
        if (!all)
        {
            var go = Modal.Confirm("Records not visible yet",
                $"Not every {(_records.Any(r => r.Type == "CNAME") ? "CNAME" : "TXT")} record is visible on the public resolvers yet. If the certificate authority can't see them the validation will fail " +
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
