using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace CertificateGet.UI;

public enum ModalKind { Info, Success, Warning, Error, Question }

public partial class ModalDialog : Window
{
    public string? Result { get; private set; }

    public ModalDialog(string title, string message, ModalKind kind, IReadOnlyList<string> buttons, int primaryIndex, bool danger)
    {
        InitializeComponent();
        TitleText.Text = title;
        MessageText.Text = message;

        var (glyph, fg, bg) = kind switch
        {
            ModalKind.Success => ("", "#15803D", "#DCFCE7"),
            ModalKind.Warning => ("", "#B45309", "#FEF3C7"),
            ModalKind.Error => ("", "#B91C1C", "#FEE2E2"),
            ModalKind.Question => ("", "#1D4ED8", "#DBEAFE"),
            _ => ("", "#1D4ED8", "#DBEAFE")
        };
        IconGlyph.Text = glyph;
        IconGlyph.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(fg));
        IconCircle.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(bg));

        for (var i = 0; i < buttons.Count; i++)
        {
            var text = buttons[i];
            var isPrimary = i == primaryIndex;
            var b = new Button
            {
                Content = text,
                MinWidth = 90,
                Margin = new Thickness(8, 0, 0, 0),
                Style = (Style)FindResource(isPrimary ? (danger ? "DangerButton" : "PrimaryButton") : "SecondaryButton"),
                IsDefault = isPrimary,
                IsCancel = !isPrimary && i == buttons.Count - 1
            };
            b.Click += (_, _) => { Result = text; DialogResult = true; };
            ButtonsPanel.Children.Add(b);
        }

        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { Result = null; DialogResult = false; } };
    }
}

/// <summary>All user-facing messages go through these modal popups (no MessageBox).</summary>
public static class Modal
{
    public static bool? ShowWindow(Window dialog)
    {
        // Parent to the active window so dialogs opened from other dialogs stack correctly.
        var owner = Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive && w != dialog)
                    ?? Application.Current?.MainWindow;
        if (owner != null && owner.IsLoaded && !ReferenceEquals(owner, dialog))
            dialog.Owner = owner;
        else
            dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        var main = Application.Current?.MainWindow as MainWindow;
        main?.SetDimmed(true);
        try { return dialog.ShowDialog(); }
        finally { main?.SetDimmed(false); }
    }

    public static string? Show(string title, string message, ModalKind kind, string[] buttons, int primaryIndex = 0, bool danger = false)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == false)
            return Application.Current.Dispatcher.Invoke(() => Show(title, message, kind, buttons, primaryIndex, danger));
        var dlg = new ModalDialog(title, message, kind, buttons, primaryIndex, danger);
        ShowWindow(dlg);
        return dlg.Result;
    }

    public static void Info(string title, string message) => Show(title, message, ModalKind.Info, new[] { "OK" });
    public static void Success(string title, string message) => Show(title, message, ModalKind.Success, new[] { "OK" });
    public static void Warning(string title, string message) => Show(title, message, ModalKind.Warning, new[] { "OK" });
    public static void Error(string title, string message) => Show(title, message, ModalKind.Error, new[] { "OK" });

    public static bool Confirm(string title, string message, string yes = "Yes", string no = "Cancel", bool danger = false) =>
        Show(title, message, danger ? ModalKind.Warning : ModalKind.Question, new[] { yes, no }, 0, danger) == yes;
}
