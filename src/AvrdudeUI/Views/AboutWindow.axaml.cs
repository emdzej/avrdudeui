using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using AvrdudeUI.Core;

namespace AvrdudeUI.Views;

public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var about =
            $"{AssemblyData.title}{Environment.NewLine}" +
            $"Version {AssemblyData.version} ({GetBuildDate()}){Environment.NewLine}" +
            $"{AssemblyData.copyright}";
        AboutText.Text = about;
    }

    private void OnOkClick(object sender, RoutedEventArgs e) => Close();

    // AVRDUDESS ships a build date encoded in Version.Build as "days since 2000-01-01".
    // Preserved for source-compatibility with the original.
    private static string GetBuildDate()
    {
        try
        {
            var v = AssemblyData.version;
            var buildDate = new DateTime(2000, 1, 1).Add(TimeSpan.FromDays(v.Build));
            return buildDate.ToString("dd-MMM-yyyy");
        }
        catch
        {
            return "unknown";
        }
    }
}
