using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using CertificateGet.Models;

namespace CertificateGet.UI;

/// <summary>Maps a status/level to a badge colour. Accepts "ok|warn|bad|none|info", LogLevel or AcmeEnvironment.</summary>
public class LevelBrushConverter : IValueConverter
{
    public string Part { get; set; } = "Foreground";

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value switch
        {
            LogLevel.Success => "ok",
            LogLevel.Warning => "warn",
            LogLevel.Error => "bad",
            LogLevel.Info => "info",
            AcmeEnvironment.Production => "ok",
            AcmeEnvironment.Staging => "warn",
            string s => s,
            _ => "none"
        };
        var bg = Part == "Background";
        var hex = key switch
        {
            "ok" => bg ? "#DCFCE7" : "#15803D",
            "warn" => bg ? "#FEF3C7" : "#B45309",
            "bad" => bg ? "#FEE2E2" : "#B91C1C",
            "info" => bg ? "#DBEAFE" : "#1D4ED8",
            _ => bg ? "#F1F5F9" : "#475569"
        };
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public static class Shell
{
    public static void OpenFolder(string path)
    {
        if (Directory.Exists(path))
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
        else
            Modal.Warning("Folder not found", path);
    }

    public static void SelectFile(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

    public static void CopyToClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch { /* clipboard busy — ignore */ }
    }
}
