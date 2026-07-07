using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvrdudeUI.Core;

namespace AvrdudeUI.Views;

public partial class PresetManagerWindow : Window
{
    // Populated by the caller before ShowDialog. currentSettings is the snapshot
    // captured from the MainWindow at open time so New/Overwrite can save "the current UI state" as a preset.
    public PresetData currentSettings;
    public Presets presets;

    public PresetManagerWindow() { InitializeComponent(); }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (presets is null) return;

        CbPresets.ItemsSource = presets.BindingSource;
        CbPresets.DisplayMemberBinding = new Avalonia.Data.Binding("name");
        CbPresets.SelectedIndex = presets.BindingSource.Count > 0 ? 0 : -1;

        ReloadExportList();
    }

    private void ReloadExportList()
    {
        // Snapshot into a fresh list so the ListBox binding rebuilds cleanly.
        LbExport.ItemsSource = null;
        LbExport.ItemsSource = presets.Items;
        LbExport.DisplayMemberBinding = new Avalonia.Data.Binding("name");
    }

    private async Task<string> PromptTextAsync(string title, string prefill)
    {
        var dlg = new EnterTextDialog(title, prefill);
        await dlg.ShowDialog(this);
        return dlg.ConfirmedOk ? dlg.InputText : null;
    }

    private async void OnNewClick(object sender, RoutedEventArgs e)
    {
        while (true)
        {
            var name = await PromptTextAsync("New preset name", "");
            if (name is null) return;
            name = name.Trim();
            if (name.Length < 1) continue;
            if (name == "Default")
            {
                MsgBox.notice("_CANTUSEDEFAULT");
                continue;
            }

            var existing = presets.Items.Find(s => s.name == name);
            if (existing != null)
            {
                if (MsgBox.confirm("_OVERWRITEPRESET", name) != MsgBoxResult.Ok)
                    continue;
                presets.Remove(existing);
            }

            var newPreset = new PresetData(currentSettings) { name = name };
            presets.Add(newPreset);
            presets.Save();

            CbPresets.SelectedItem = presets.Items.Find(s => s.name == name);
            ReloadExportList();
            return;
        }
    }

    private async void OnRenameClick(object sender, RoutedEventArgs e)
    {
        if (CbPresets.SelectedItem is not PresetData selected) return;

        while (true)
        {
            var name = await PromptTextAsync("Rename preset", selected.name);
            if (name is null) return;
            name = name.Trim();
            if (name == selected.name) return;
            if (name.Length < 1) continue;
            if (name == "Default")
            {
                MsgBox.notice("_CANTUSEDEFAULT");
                continue;
            }

            var clash = presets.Items.Find(s => s.name == name);
            if (clash != null)
            {
                if (MsgBox.confirm("_PRESETALREADYEXISTS") != MsgBoxResult.Ok)
                    continue;
                presets.Remove(clash);
            }

            selected.name = name;
            presets.Save();
            ReloadExportList();
            return;
        }
    }

    private void OnOverwriteClick(object sender, RoutedEventArgs e)
    {
        if (CbPresets.SelectedItem is not PresetData selected) return;

        if (selected.name == "Default")
        {
            MsgBox.notice("_CANTOVERWRITEDEFAULT");
            return;
        }

        if (MsgBox.confirm("_PRESETOVERWRITE", selected.name) != MsgBoxResult.Ok) return;

        var name = selected.name;
        selected.copyFrom(currentSettings);
        selected.name = name;
        presets.Save();

        CbPresets.SelectedItem = presets.Items.Find(s => s.name == name);
        ReloadExportList();
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        var toDelete = LbExport.SelectedItems
            .OfType<PresetData>()
            .Where(p => p.name != "Default")
            .ToList();

        if (toDelete.Count == 0) return;

        if (MsgBox.confirm("_DELETEPRESETS", toDelete.Count) != MsgBoxResult.Ok) return;

        foreach (var p in toDelete) presets.Remove(p);
        presets.Save();
        ReloadExportList();
    }

    private async void OnExportClick(object sender, RoutedEventArgs e)
    {
        var opts = new FilePickerSaveOptions
        {
            Title = "Export presets",
            SuggestedFileName = "presets.xml",
            DefaultExtension = "xml",
            FileTypeChoices = new[]
            {
                new FilePickerFileType("XML") { Patterns = new[] { "*.xml" } },
                FilePickerFileTypes.All
            }
        };

        var file = await StorageProvider.SaveFilePickerAsync(opts);
        if (file is null) return;

        var path = file.TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        var export = new Presets(path);
        foreach (var item in LbExport.SelectedItems.OfType<PresetData>())
        {
            export.Add(item);
            Util.consoleWriteLine("_EXPORTINGPRESETS", item.name);
        }
        export.Save();
        Util.consoleWriteLine("_EXPORTCOMPLETE");
    }

    private async void OnImportClick(object sender, RoutedEventArgs e)
    {
        var picked = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Import presets",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("XML") { Patterns = new[] { "*.xml" } },
                FilePickerFileTypes.All
            }
        });

        if (picked.Count == 0) return;
        var path = picked[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        var import = new Presets(path);
        import.Load();

        foreach (var np in import.Items)
        {
            Util.consoleWriteLine("_IMPORTINGPRESETS", np.name);

            // Duplicate name → suffix with a unique tick stamp to preserve both.
            if (presets.Items.Find(s => s.name == np.name) != null)
            {
                var oldName = np.name;
                np.name = $"{np.name} {DateTime.UtcNow.Ticks:X16}";
                Util.consoleWarning("_IMPORTALREADYEXISTS", oldName, np.name);
            }
            presets.Add(np);
        }

        presets.Save();
        ReloadExportList();
        Util.consoleWriteLine("_IMPORTCOMPLETE");
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
