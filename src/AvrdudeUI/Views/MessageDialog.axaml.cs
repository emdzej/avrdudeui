using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace AvrdudeUI.Views;

public partial class MessageDialog : Window
{
    public enum Kind { Info, Warning, Error, Confirm }

    public bool ConfirmedOk { get; private set; }

    public MessageDialog()
    {
        InitializeComponent();
    }

    public MessageDialog(string title, string message, Kind kind) : this()
    {
        Title = title;
        MessageText.Text = message;

        switch (kind)
        {
            case Kind.Error:
                IconGlyph.Text = "✗"; // ✗
                IconGlyph.Foreground = new SolidColorBrush(Color.FromRgb(0xd0, 0x40, 0x40));
                break;
            case Kind.Warning:
                IconGlyph.Text = "!";
                IconGlyph.Foreground = new SolidColorBrush(Color.FromRgb(0xd0, 0xa0, 0x30));
                break;
            case Kind.Confirm:
                IconGlyph.Text = "?";
                IconGlyph.Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x90, 0xd0));
                BtnCancel.IsVisible = true;
                break;
            case Kind.Info:
            default:
                IconGlyph.Text = "i";
                IconGlyph.Foreground = new SolidColorBrush(Color.FromRgb(0x40, 0x90, 0xd0));
                break;
        }
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        ConfirmedOk = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        ConfirmedOk = false;
        Close();
    }
}
