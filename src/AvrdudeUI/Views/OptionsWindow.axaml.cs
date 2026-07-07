using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvrdudeUI.Core;

namespace AvrdudeUI.Views;

public partial class OptionsWindow : Window
{
    public bool ConfirmedOk { get; private set; }

    // These properties are the surface the caller reads back after ShowDialog completes.
    public bool toolTips
    {
        get => CbShowToolTips.IsChecked == true;
        set => CbShowToolTips.IsChecked = value;
    }
    public bool usePreviousSettings
    {
        get => CbUsePrevSettings.IsChecked == true;
        set => CbUsePrevSettings.IsChecked = value;
    }
    public bool checkForUpdates
    {
        get => CbCheckForUpdate.IsChecked == true;
        set => CbCheckForUpdate.IsChecked = value;
    }
    public string avrdudeLocation
    {
        get => TxtAvrdude.Text ?? string.Empty;
        set => TxtAvrdude.Text = value ?? string.Empty;
    }
    public string avrdudeConfLocation
    {
        get => TxtAvrdudeConf.Text ?? string.Empty;
        set => TxtAvrdudeConf.Text = value ?? string.Empty;
    }
    public string avrSizeLocation
    {
        get => TxtAvrSize.Text ?? string.Empty;
        set => TxtAvrSize.Text = value ?? string.Empty;
    }

    public string language
    {
        get => (CbLanguage.SelectedItem as LanguageItem)?.Key ?? "english";
        set
        {
            foreach (var item in CbLanguage.Items.OfType<LanguageItem>())
            {
                if (item.Key == value) { CbLanguage.SelectedItem = item; return; }
            }
        }
    }

    public HashSetD<string> hiddenProgrammers
    {
        get => ReadHiddenIds(LbHiddenProgrammers);
        set => ApplyHiddenIds(LbHiddenProgrammers, value);
    }

    public HashSetD<string> hiddenMCUs
    {
        get => ReadHiddenIds(LbHiddenMCUs);
        set => ApplyHiddenIds(LbHiddenMCUs, value);
    }

    private bool _checkAllProg;
    private bool _checkAllMcu;

    public OptionsWindow() { InitializeComponent(); }

    public OptionsWindow(IEnumerable<Programmer> programmers, IEnumerable<MCU> mcus) : this()
    {
        // Language picker
        CbLanguage.ItemsSource = Language.Translation.Languages
            .Select(kv => new LanguageItem(kv.Key, kv.Value))
            .ToList();

        // Hidden-programmer list (id + description)
        LbHiddenProgrammers.ItemsSource = programmers
            .Select(p => new HiddenPartItem(p.id, $"{p.id} . . . ({p.desc})"))
            .ToList();

        // Hidden-MCU list (desc + id)
        LbHiddenMCUs.ItemsSource = mcus
            .Select(m => new HiddenPartItem(m.id, $"{m.desc} ({m.id})"))
            .ToList();

        Opened += (_, _) =>
        {
            // Match original: first click on "Check/Uncheck" checks all if <half selected.
            _checkAllProg = LbHiddenProgrammers.SelectedItems.Count < LbHiddenProgrammers.ItemCount / 2;
            _checkAllMcu  = LbHiddenMCUs.SelectedItems.Count       < LbHiddenMCUs.ItemCount       / 2;
        };
    }

    private static HashSetD<string> ReadHiddenIds(ListBox lb)
    {
        var result = new HashSetD<string>();
        foreach (var item in lb.SelectedItems.OfType<HiddenPartItem>())
            result.Add(item.Id);
        return result;
    }

    private static void ApplyHiddenIds(ListBox lb, HashSetD<string> hidden)
    {
        if (hidden is null || lb.ItemsSource is not IEnumerable<HiddenPartItem> items) return;

        lb.SelectedItems.Clear();
        foreach (var item in items)
            if (hidden.Contains(item.Id))
                lb.SelectedItems.Add(item);
    }

    private void OnToggleAllProgrammers(object sender, RoutedEventArgs e)
    {
        ToggleAll(LbHiddenProgrammers, _checkAllProg);
        _checkAllProg = !_checkAllProg;
    }

    private void OnToggleAllMCUs(object sender, RoutedEventArgs e)
    {
        ToggleAll(LbHiddenMCUs, _checkAllMcu);
        _checkAllMcu = !_checkAllMcu;
    }

    private static void ToggleAll(ListBox lb, bool selectAll)
    {
        lb.SelectedItems.Clear();
        if (!selectAll) return;
        if (lb.ItemsSource is IEnumerable<HiddenPartItem> items)
            foreach (var item in items)
                lb.SelectedItems.Add(item);
    }

    private async void OnBrowseAvrdude(object sender, RoutedEventArgs e) =>
        await Browse("Locate avrdude", TxtAvrdude);
    private async void OnBrowseAvrdudeConf(object sender, RoutedEventArgs e) =>
        await Browse("Locate avrdude.conf", TxtAvrdudeConf, new FilePickerFileType("conf") { Patterns = new[] { "*.conf" } });
    private async void OnBrowseAvrSize(object sender, RoutedEventArgs e) =>
        await Browse("Locate avr-size", TxtAvrSize);

    private async System.Threading.Tasks.Task Browse(string title, TextBox target, FilePickerFileType filter = null)
    {
        var opts = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        };
        if (filter is not null)
            opts.FileTypeFilter = new[] { filter, FilePickerFileTypes.All };

        // Seed the picker with the directory of the currently-selected path (if any).
        try
        {
            var dir = Path.GetDirectoryName(target.Text);
            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                opts.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(dir);
        }
        catch { /* Bad path — ignore, picker opens in default location */ }

        var picked = await StorageProvider.OpenFilePickerAsync(opts);
        if (picked.Count > 0)
            target.Text = picked[0].TryGetLocalPath();
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

    // ListBox item wrappers. Named types (rather than tuples) so binding + string
    // display is unambiguous in Avalonia's compiled bindings.
    private sealed record LanguageItem(string Key, string Display)
    {
        public override string ToString() => Display;
    }

    private sealed record HiddenPartItem(string Id, string Display)
    {
        public override string ToString() => Display;
    }
}
