using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AvrdudeUI.Core;
using AvrdudeUI.Services;
using AvrdudeUI.Views;

namespace AvrdudeUI;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Config + Language load before we surface any windows so early
            // Language.Translation.get() lookups resolve against a populated dictionary.
            Config.Load();
            Language.Translation.Load();

            var mainWindow = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen
            };
            desktop.MainWindow = mainWindow;

            AppConsole.SetSink(new UiConsoleSink(mainWindow.LogTextBlock, mainWindow.LogScrollViewer));
            AppMsgBox.SetProvider(new UiMsgBoxProvider());

            Util.consoleWriteLine($"AvrdudeUI {AssemblyData.version} — ready");
            Util.consoleWriteLine($"Platform: {(PlatformUtil.IsMac ? "macOS" : PlatformUtil.IsLinux ? "Linux" : "Windows")}");
            Util.consoleWriteLine($"Config: {AssemblyData.AppDataDir}");
        }

        base.OnFrameworkInitializationCompleted();
    }
}
