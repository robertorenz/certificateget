using System.Windows;
using System.Windows.Input;
using CertificateGet.Services;

namespace CertificateGet.UI;

/// <summary>Lists the four manual volumes in English or Spanish; clicking one opens it in the browser.</summary>
public partial class HelpDialog : Window
{
    // Remembered for the rest of the session, so F1 opens in the language last chosen.
    private static string _lang = HelpDocs.DefaultLanguage;

    public HelpDialog()
    {
        InitializeComponent();
        (_lang == HelpDocs.Spanish ? LangEs : LangEn).IsChecked = true;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    private void Lang_Changed(object sender, RoutedEventArgs e)
    {
        _lang = LangEs.IsChecked == true ? HelpDocs.Spanish : HelpDocs.English;
        var es = _lang == HelpDocs.Spanish;
        VolumeList.ItemsSource = HelpDocs.Volumes(_lang);
        TitleText.Text = es ? "Manual de CertificateGet" : "CertificateGet manual";
        SubtitleText.Text = es ? "Cuatro volúmenes. Cada uno se abre en el navegador y enlazan entre sí."
                               : "Four volumes. Each opens in your browser, and they link to each other.";
        FooterText.Text = es ? "Pulse F1 en cualquier lugar para abrir esta lista." : "Press F1 anywhere to open this list.";
        CloseButton.Content = es ? "Cerrar" : "Close";
    }

    private void Volume_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not HelpDocs.Volume v) return;
        try
        {
            HelpDocs.Open(v.File, _lang);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Modal.Error(_lang == HelpDocs.Spanish ? "No se pudo abrir el manual" : "Could not open the manual", ex.Message);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
