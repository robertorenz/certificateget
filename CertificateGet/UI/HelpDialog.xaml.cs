using System.Windows;
using System.Windows.Input;
using CertificateGet.Services;

namespace CertificateGet.UI;

/// <summary>Lists the four manual volumes; clicking one opens it in the browser.</summary>
public partial class HelpDialog : Window
{
    public HelpDialog()
    {
        InitializeComponent();
        VolumeList.ItemsSource = HelpDocs.Volumes;
        MouseLeftButtonDown += (_, e) => { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); };
    }

    private void Volume_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not HelpDocs.Volume v) return;
        try
        {
            HelpDocs.Open(v.File);
            DialogResult = true;
        }
        catch (Exception ex)
        {
            Modal.Error("Could not open the manual", ex.Message);
        }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
