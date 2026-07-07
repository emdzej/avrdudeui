using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvrdudeUI.Core;

namespace AvrdudeUI.Views;

public partial class UpdateWindow : Window
{
    // Delay before buttons enable — matches original AVRDUDESS behavior:
    // prevents an accidental click if the dialog steals focus while the user
    // is mid-keystroke.
    private static readonly TimeSpan ArmDelay = TimeSpan.FromSeconds(2);

    private readonly string _updateAddress;
    private DispatcherTimer _armTimer;
    private bool _armed;

    public event EventHandler OnSkipVersion;

    public UpdateWindow() { InitializeComponent(); }

    public UpdateWindow(UpdateData data) : this()
    {
        _updateAddress = data.updateAddr;

        LblCurrent.Text = data.currentVersion.ToString();

        var latest = data.Latest;
        LblLatest.Text = latest is null
            ? "(unknown)"
            : $"{latest.Version} ({latest.Date.ToLocalTime().ToLongDateString()})";

        var info = string.Empty;
        data.releases.ForEach(r =>
        {
            info += $"v{r.Version} ({r.Date.ToLocalTime().ToLongDateString()}){Environment.NewLine}" +
                    $"{r.info}{Environment.NewLine}{Environment.NewLine}";
        });
        TxtInfo.Text = info.Replace("\r\n", "\n").Replace("\n", Environment.NewLine);

        Opened += (_, _) => StartArmTimer();
        Closing += (_, e) =>
        {
            if (!_armed) e.Cancel = true;
        };
    }

    private void StartArmTimer()
    {
        _armTimer = new DispatcherTimer { Interval = ArmDelay };
        _armTimer.Tick += (_, _) =>
        {
            _armTimer.Stop();
            _armed = true;
            BtnUpdate.IsEnabled = true;
            BtnSkip.IsEnabled = true;
            BtnLater.IsEnabled = true;
        };
        _armTimer.Start();
    }

    private void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_updateAddress))
            PlatformUtil.OpenUrl(_updateAddress);
        Close();
    }

    private void OnLaterClick(object sender, RoutedEventArgs e) => Close();

    private void OnSkipClick(object sender, RoutedEventArgs e)
    {
        OnSkipVersion?.Invoke(this, EventArgs.Empty);
        Close();
    }
}
