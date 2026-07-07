using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AvrdudeUI.Views;

public partial class EnterTextDialog : Window
{
    public bool ConfirmedOk { get; private set; }

    public string InputText
    {
        get => InputBox.Text ?? string.Empty;
        set => InputBox.Text = value ?? string.Empty;
    }

    public EnterTextDialog() { InitializeComponent(); }

    public EnterTextDialog(string title, string prefill) : this()
    {
        Title = title;
        InputText = prefill;
        InputBox.SelectAll();
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
