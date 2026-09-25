using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using CertificateGet.Models;
using CertificateGet.Services;
using CertificateGet.UI;
using Microsoft.Win32;

namespace CertificateGet.Views;

public partial class ActivityLogView : UserControl
{
    private List<LogEntry> _all = new();

    public ActivityLogView()
    {
        InitializeComponent();
        Grid.MouseDoubleClick += (_, _) =>
        {
            if (Grid.SelectedItem is LogEntry e)
                Modal.Show($"{e.Level} · {e.Category}", $"{e.TimeDisplay}\n{(e.Certificate != null ? "Certificate: " + e.Certificate + "\n" : "")}\n{e.Message}",
                    e.Level switch { LogLevel.Error => ModalKind.Error, LogLevel.Warning => ModalKind.Warning, LogLevel.Success => ModalKind.Success, _ => ModalKind.Info },
                    new[] { "Close" });
        };
        ActivityLog.EntryAdded += e => Dispatcher.BeginInvoke(() =>
        {
            if (!IsVisible) return;
            _all.Insert(0, e);
            ApplyFilter();
        });
    }

    public void Reload()
    {
        _all = ActivityLog.ReadAll();
        _all.Reverse();
        ApplyFilter();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e) => ApplyFilter();

    private IEnumerable<LogEntry> Filtered()
    {
        IEnumerable<LogEntry> q = _all;
        var level = (LevelBox?.SelectedItem as ComboBoxItem)?.Content as string;
        if (level != null && level != "All levels") q = q.Where(x => x.Level.ToString() == level);
        var text = SearchBox?.Text.Trim() ?? "";
        if (text.Length > 0)
            q = q.Where(x => x.Message.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                             x.Category.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                             (x.Certificate?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false));
        return q;
    }

    private void ApplyFilter()
    {
        if (Grid == null) return;
        var list = Filtered().ToList();
        Grid.ItemsSource = list;
        CountText.Text = $"{list.Count} of {_all.Count} entries";
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        if (File.Exists(SettingsService.LogFile)) Shell.SelectFile(SettingsService.LogFile);
        else Modal.Info("No log yet", "The log file will be created with the first recorded activity.");
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Filter = "CSV file|*.csv", FileName = $"certificateget-log-{DateTime.Now:yyyyMMdd}.csv" };
        if (dlg.ShowDialog() != true) return;
        static string Q(string? s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
        var sb = new StringBuilder("Time,Level,Category,Certificate,Message\r\n");
        foreach (var x in Filtered())
            sb.Append(Q(x.TimeDisplay)).Append(',').Append(Q(x.Level.ToString())).Append(',').Append(Q(x.Category)).Append(',')
              .Append(Q(x.Certificate)).Append(',').Append(Q(x.Message)).Append("\r\n");
        File.WriteAllText(dlg.FileName, sb.ToString(), new UTF8Encoding(true));
        var r = Modal.Show("Log exported", dlg.FileName, ModalKind.Success, new[] { "Open", "Close" });
        if (r == "Open") Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
    }
}
