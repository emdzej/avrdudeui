using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using AvrdudeUI.Core;

namespace AvrdudeUI.Views;

// Main workspace window — the Avalonia analogue of Form1 from AVRDUDESS.
//
// Design:
//   • All Core state lives on `_avrdude`, `_avrsize`, `_presets`, `_flashFile`, `_eepromFile`.
//   • The command-line preview and enable/disable logic derive from a re-populated
//     AvrdudeSettings each time `RegenerateCmdLine()` runs (called on every property change).
//   • Long-running work (avrdude load, avrdude launch) runs on a Task; the process
//     writes to stderr which is streamed via AppConsole (see UiConsoleSink).
public partial class MainWindow : Window
{
    private Avrdude _avrdude;
    private Avrsize _avrsize;
    private Presets _presets;
    private CmdLine _cmdLine;
    private AvrdudeSettings _settings;
    private MemTypeFile _flashFile;
    private MemTypeFile _eepromFile;

    private string _flashOp = FileOp.Write;
    private string _eepromOp = FileOp.Write;
    private string _lastBitClock = string.Empty;
    private Avrdude.UsbAspFreq _lastUsbAspFreq;
    private bool _suppressChangeEvents;
    private bool _loaded;

    public MainWindow()
    {
        InitializeComponent();

        // Populate static combo boxes early — safe to do before Config/Language load.
        CbFlashFormat.ItemsSource = Avrdude.fileFormats;
        CbEepromFormat.ItemsSource = Avrdude.fileFormats;
        CbFlashFormat.DisplayMemberBinding = new Avalonia.Data.Binding("Desc");
        CbEepromFormat.DisplayMemberBinding = new Avalonia.Data.Binding("Desc");
        CbFlashFormat.SelectedIndex = 0;
        CbEepromFormat.SelectedIndex = 0;

        CbUsbAspFreq.ItemsSource = Avrdude.USBaspFreqs;
        CbUsbAspFreq.DisplayMemberBinding = new Avalonia.Data.Binding("name");

        CbVerbosity.ItemsSource = new[] { 0, 1, 2, 3, 4, 5 };
        CbVerbosity.SelectedIndex = 0;

        // Live command-line regen on any input change
        WireChangeHandlers();

        Opened += OnWindowOpened;
        Closing += OnWindowClosing;
    }

    // Exposed so App.axaml.cs can plug in the UiConsoleSink before we start writing to the log.
    public SelectableTextBlock LogTextBlock => LogText;
    public ScrollViewer LogScrollViewer => LogScroll;

    private void WireChangeHandlers()
    {
        CbProgrammer.SelectionChanged += OnProgrammerChanged;
        CbMcu.SelectionChanged += OnMcuChanged;
        CbPort.SelectionChanged += (_, __) => RegenerateCmdLine();
        CbPort.LostFocus         += (_, __) => RegenerateCmdLine();
        TxtBaud.TextChanged      += (_, __) => RegenerateCmdLine();
        TxtBitClock.TextChanged  += (_, __) => RegenerateCmdLine();
        TxtLFuse.TextChanged     += (_, __) => RegenerateCmdLine();
        TxtHFuse.TextChanged     += (_, __) => RegenerateCmdLine();
        TxtEFuse.TextChanged     += (_, __) => RegenerateCmdLine();
        TxtLock.TextChanged      += (_, __) => RegenerateCmdLine();
        TxtAdditional.TextChanged += (_, __) => RegenerateCmdLine();

        TxtFlashFile.TextChanged += OnFlashFileTextChanged;
        TxtEepromFile.TextChanged += OnEepromFileTextChanged;

        CbFlashFormat.SelectionChanged += (_, __) => RegenerateCmdLine();
        CbEepromFormat.SelectionChanged += (_, __) => RegenerateCmdLine();
        CbVerbosity.SelectionChanged    += (_, __) => RegenerateCmdLine();

        foreach (var cb in new[] { CbForce, CbNoVerify, CbDisableFlashErase, CbEraseBoth, CbDoNotWrite, CbSetFuses, CbSetLock })
        {
            cb.IsCheckedChanged += (_, __) => RegenerateCmdLine();
        }
    }

    private async void OnWindowOpened(object sender, EventArgs e)
    {
        // Restore persisted window geometry (defensive against garbage/first-run).
        try
        {
            var loc = Config.Prop.windowLocation;
            var size = Config.Prop.windowSize;
            if (size.Width >= (int)MinWidth && size.Height >= (int)MinHeight)
            {
                Width = size.Width;
                Height = size.Height;
            }
            if (loc.X > 0 || loc.Y > 0)
                Position = new Avalonia.PixelPoint(loc.X, loc.Y);
        }
        catch { /* Ignore malformed persisted geometry. */ }

        LblStatus.Text = "Loading avrdude…";
        Util.consoleWriteLine("Loading avrdude…");

        await Task.Run(() =>
        {
            _avrdude = new Avrdude();
            _avrsize = new Avrsize();

            _avrdude.OnProcessStart  += (_, __) => Dispatcher.UIThread.Post(() => { LblStatus.Text = "avrdude running…"; BtnProgram.IsEnabled = false; BtnStop.IsEnabled = true; });
            _avrdude.OnProcessEnd    += (_, __) => Dispatcher.UIThread.Post(() => { LblStatus.Text = "Ready"; BtnProgram.IsEnabled = true; BtnStop.IsEnabled = false; UpdateActionButtonEnabled(); });
            _avrdude.OnVersionChange += (_, __) => Dispatcher.UIThread.Post(UpdateVersionLabel);
            _avrdude.OnDetectedMCU   += OnDetectedMcu;
            _avrdude.OnReadFuseLock  += OnReadFuseLock;

            _avrdude.load();
            _avrsize.load();

            _flashFile = new MemTypeFile(_avrsize);
            _eepromFile = new MemTypeFile(_avrsize);
            _flashFile.sizeChanged += OnFlashFileSizeChanged;
            _eepromFile.sizeChanged += OnEepromFileSizeChanged;

            _presets = new Presets();
            _presets.Load();
        });

        // Rehydrate translations for file-format descriptions (they use _KEY lookups)
        Avrdude.fileFormats.ForEach(f => f.ApplyTranslation());
        CbFlashFormat.ItemsSource = null;
        CbFlashFormat.ItemsSource = Avrdude.fileFormats;
        CbFlashFormat.SelectedIndex = 0;
        CbEepromFormat.ItemsSource = null;
        CbEepromFormat.ItemsSource = Avrdude.fileFormats;
        CbEepromFormat.SelectedIndex = 0;

        PopulateProgrammersAndMcus();
        PopulatePorts();
        PopulatePresets();

        _settings = new AvrdudeSettings();
        _cmdLine = new CmdLine(_settings);

        // Apply saved previous session settings, if the user opted in
        if (Config.Prop.usePreviousSettings && Config.Prop.previousSettings != null)
            LoadPresetData(Config.Prop.previousSettings);

        _loaded = true;
        UpdateVersionLabel();
        RegenerateCmdLine();
    }

    private void UpdateVersionLabel()
    {
        LblAvrdudeVersion.Text = string.IsNullOrEmpty(_avrdude?.version)
            ? "avrdude: not found — brew install avrdude"
            : _avrdude.version;
    }

    private void OnWindowClosing(object sender, Avalonia.Controls.WindowClosingEventArgs e)
    {
        try
        {
            Config.Prop.windowLocation = new WindowPoint { X = Position.X, Y = Position.Y };
            Config.Prop.windowSize = new WindowSize { Width = (int)Width, Height = (int)Height };
            Config.Prop.previousSettings = Config.Prop.usePreviousSettings
                ? MakePresetData("")
                : new PresetData();
            Config.Save();
        }
        catch { /* best-effort persistence — don't block the close on it. */ }
    }

    private void PopulateProgrammersAndMcus()
    {
        var progs = _avrdude.programmers.FindAll(p => !p.hide);
        progs.Insert(0, new Programmer("", "-- select programmer --"));
        var mcus = _avrdude.mcus.FindAll(m => !m.hide);
        mcus.Insert(0, new MCU("", "-- select MCU --"));

        CbProgrammer.ItemsSource = progs;
        CbProgrammer.DisplayMemberBinding = new Avalonia.Data.Binding("desc");
        CbProgrammer.SelectedIndex = 0;

        CbMcu.ItemsSource = mcus;
        CbMcu.DisplayMemberBinding = new Avalonia.Data.Binding("desc");
        CbMcu.SelectedIndex = 0;
    }

    private void PopulatePorts()
    {
        var current = CbPort.SelectedItem as string ?? CbPort.Text;
        var ports = SerialPortEnumerator.List();
        CbPort.ItemsSource = ports;
        if (!string.IsNullOrEmpty(current))
            CbPort.Text = current;
    }

    private void PopulatePresets()
    {
        CbPresets.ItemsSource = _presets.BindingSource;
        CbPresets.DisplayMemberBinding = new Avalonia.Data.Binding("name");
        var def = _presets.Items.Find(p => p.name == "Default");
        if (def != null) CbPresets.SelectedItem = def;
    }

    // ---------- input handlers ----------

    private void OnProgrammerChanged(object sender, SelectionChangedEventArgs e)
    {
        // USBasp uses `-B` as a divisor code, not a µs bit-clock, so swap the
        // bit-clock textbox for a preset combo when the user picks a USBasp variant.
        var prog = CbProgrammer.SelectedItem as Programmer;
        var isUsbAsp = prog != null && prog.id.StartsWith("usbasp");
        if (isUsbAsp && TxtBitClock.IsVisible)
        {
            _lastBitClock = TxtBitClock.Text ?? string.Empty;
            TxtBitClock.IsVisible = false;
            CbUsbAspFreq.IsVisible = true;
            CbUsbAspFreq.SelectedItem = _lastUsbAspFreq ?? Avrdude.USBaspFreqs[0];
        }
        else if (!isUsbAsp && !TxtBitClock.IsVisible)
        {
            _lastUsbAspFreq = CbUsbAspFreq.SelectedItem as Avrdude.UsbAspFreq;
            TxtBitClock.IsVisible = true;
            CbUsbAspFreq.IsVisible = false;
            TxtBitClock.Text = _lastBitClock;
        }

        RegenerateCmdLine();
    }

    private void OnMcuChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CbMcu.SelectedItem is MCU m && !string.IsNullOrEmpty(m.id))
        {
            LblFlashUsage.Text = $"flash: {PlatformUtil.FileSizeFormat(m.flash)} · sig {m.signature.ToUpper()}";
            LblEepromUsage.Text = $"eeprom: {PlatformUtil.FileSizeFormat(m.eeprom)}";

            // Enable/disable fuse fields per MCU memory map
            TxtLFuse.IsEnabled = m.memoryTypes.Contains("lfuse") || m.memoryTypes.Contains("fuse");
            TxtHFuse.IsEnabled = m.memoryTypes.Contains("hfuse");
            TxtEFuse.IsEnabled = m.memoryTypes.Contains("efuse");
        }
        else
        {
            LblFlashUsage.Text = "";
            LblEepromUsage.Text = "";
        }
        RegenerateCmdLine();
    }

    private void OnFlashFileTextChanged(object sender, EventArgs e)
    {
        if (_flashFile != null) _flashFile.location = TxtFlashFile.Text ?? string.Empty;
        RegenerateCmdLine();
    }

    private void OnEepromFileTextChanged(object sender, EventArgs e)
    {
        if (_eepromFile != null) _eepromFile.location = TxtEepromFile.Text ?? string.Empty;
        RegenerateCmdLine();
    }

    private void OnFlashFileSizeChanged(object sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var m = CbMcu.SelectedItem as MCU;
            var sz = _flashFile.size;
            if (sz != Avrsize.INVALID && m != null && m.flash > 0)
            {
                var pct = 100f * sz / m.flash;
                LblFlashUsage.Text = $"flash: {sz:#,0} / {m.flash:#,0} B ({pct:0.0}%)";
            }
        });
    }

    private void OnEepromFileSizeChanged(object sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var m = CbMcu.SelectedItem as MCU;
            var sz = _eepromFile.size;
            if (sz != Avrsize.INVALID && m != null && m.eeprom > 0)
            {
                var pct = 100f * sz / m.eeprom;
                LblEepromUsage.Text = $"eeprom: {sz:#,0} / {m.eeprom:#,0} B ({pct:0.0}%)";
            }
        });
    }

    private void OnFlashOpChanged(object sender, RoutedEventArgs e)
    {
        _flashOp = RbFlashRead.IsChecked == true ? FileOp.Read
                 : RbFlashVerify.IsChecked == true ? FileOp.Verify
                 : FileOp.Write;
        RegenerateCmdLine();
    }

    private void OnEepromOpChanged(object sender, RoutedEventArgs e)
    {
        _eepromOp = RbEepromRead.IsChecked == true ? FileOp.Read
                  : RbEepromVerify.IsChecked == true ? FileOp.Verify
                  : FileOp.Write;
        RegenerateCmdLine();
    }

    private void OnUsbAspFreqChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CbUsbAspFreq.SelectedItem is Avrdude.UsbAspFreq f)
        {
            // Only push into bit-clock when the visible input is the freq combo.
            _lastBitClock = f.bitClock ?? string.Empty;
            RegenerateCmdLine();
        }
    }

    // ---------- port refresh ----------

    private void OnPortDropDownOpened(object sender, EventArgs e) => PopulatePorts();
    private void OnRefreshPortsClick(object sender, RoutedEventArgs e) => PopulatePorts();

    // ---------- Command line regeneration ----------

    private void RegenerateCmdLine()
    {
        if (!_loaded || _settings == null || _suppressChangeEvents) return;

        _settings.prog = CbProgrammer.SelectedItem as Programmer;
        _settings.mcu = CbMcu.SelectedItem as MCU;
        _settings.port = (CbPort.SelectedItem as string) ?? CbPort.Text ?? string.Empty;
        _settings.baudRate = (TxtBaud.Text ?? string.Empty).Trim();
        _settings.bitClock = TxtBitClock.IsVisible
            ? (TxtBitClock.Text ?? string.Empty).Trim()
            : ((CbUsbAspFreq.SelectedItem as Avrdude.UsbAspFreq)?.bitClock ?? string.Empty);

        _settings.force = CbForce.IsChecked == true;
        _settings.disableVerify = CbNoVerify.IsChecked == true;
        _settings.disableFlashErase = CbDisableFlashErase.IsChecked == true;
        _settings.eraseFlashAndEEPROM = CbEraseBoth.IsChecked == true;
        _settings.doNotWrite = CbDoNotWrite.IsChecked == true;

        _settings.flashFile = (TxtFlashFile.Text ?? string.Empty).Trim();
        _settings.flashFileFormat = (CbFlashFormat.SelectedItem as FileFormat)?.Id ?? "a";
        _settings.flashFileOperation = _flashOp;

        _settings.EEPROMFile = (TxtEepromFile.Text ?? string.Empty).Trim();
        _settings.EEPROMFileFormat = (CbEepromFormat.SelectedItem as FileFormat)?.Id ?? "a";
        _settings.EEPROMFileOperation = _eepromOp;

        _settings.lowFuse = TxtLFuse.Text ?? string.Empty;
        _settings.highFuse = TxtHFuse.Text ?? string.Empty;
        _settings.exFuse = TxtEFuse.Text ?? string.Empty;
        _settings.setFuses = CbSetFuses.IsChecked == true;

        _settings.lockSetting = TxtLock.Text ?? string.Empty;
        _settings.setLock = CbSetLock.IsChecked == true;

        _settings.additionalSettings = TxtAdditional.Text ?? string.Empty;
        _settings.verbosity = (byte)(CbVerbosity.SelectedItem is int v ? v : 0);

        TxtCmdLine.Text = _cmdLine.generate();
        UpdateActionButtonEnabled();
    }

    private void UpdateActionButtonEnabled()
    {
        var haveProg = _settings?.prog != null;
        var haveMcu = _settings?.mcu != null;
        var enabled = haveProg && haveMcu;
        BtnProgram.IsEnabled = enabled && !(_avrdude?.log is null && !haveProg);
        BtnDetect.IsEnabled = haveProg;
        BtnFuseSelector.IsEnabled = enabled;
        BtnReadFuses.IsEnabled = enabled;
        BtnWriteFuses.IsEnabled = enabled;
        BtnReadLock.IsEnabled = enabled;
        BtnWriteLock.IsEnabled = enabled;
        BtnFlashGo.IsEnabled = enabled;
        BtnEepromGo.IsEnabled = enabled;
    }

    // ---------- Preset load/save ----------

    private PresetData MakePresetData(string name) => new PresetData(name)
    {
        programmer = _settings?.prog?.id ?? "",
        mcu = _settings?.mcu?.id ?? "",
        port = _settings?.port,
        baud = _settings?.baudRate,
        bitclock = _settings?.bitClock,
        flashFile = _settings?.flashFile,
        flashFormat = _settings?.flashFileFormat,
        flashOp = _settings?.flashFileOperation,
        EEPROMFile = _settings?.EEPROMFile,
        EEPROMFormat = _settings?.EEPROMFileFormat,
        EEPROMOp = _settings?.EEPROMFileOperation,
        force = _settings?.force ?? false,
        disableVerify = _settings?.disableVerify ?? false,
        disableFlashErase = _settings?.disableFlashErase ?? false,
        eraseFlashAndEEPROM = _settings?.eraseFlashAndEEPROM ?? false,
        doNotWrite = _settings?.doNotWrite ?? false,
        lfuse = _settings?.lowFuse,
        hfuse = _settings?.highFuse,
        efuse = _settings?.exFuse,
        setFuses = _settings?.setFuses ?? false,
        lockBits = _settings?.lockSetting,
        setLock = _settings?.setLock ?? false,
        additional = _settings?.additionalSettings,
        verbosity = _settings?.verbosity ?? 0
    };

    private void LoadPresetData(PresetData p)
    {
        if (p == null) return;
        _suppressChangeEvents = true;
        try
        {
            SelectByProgrammerId(p.programmer);
            SelectByMcuId(p.mcu);
            CbPort.Text = p.port ?? string.Empty;
            TxtBaud.Text = p.baud ?? string.Empty;
            TxtBitClock.Text = p.bitclock ?? string.Empty;
            TxtFlashFile.Text = p.flashFile ?? string.Empty;
            SetFileFormat(CbFlashFormat, p.flashFormat);
            SetFlashOp(p.flashOp);
            TxtEepromFile.Text = p.EEPROMFile ?? string.Empty;
            SetFileFormat(CbEepromFormat, p.EEPROMFormat);
            SetEepromOp(p.EEPROMOp);
            CbForce.IsChecked = p.force;
            CbNoVerify.IsChecked = p.disableVerify;
            CbDisableFlashErase.IsChecked = p.disableFlashErase;
            CbEraseBoth.IsChecked = p.eraseFlashAndEEPROM;
            CbDoNotWrite.IsChecked = p.doNotWrite;
            TxtLFuse.Text = p.lfuse ?? string.Empty;
            TxtHFuse.Text = p.hfuse ?? string.Empty;
            TxtEFuse.Text = p.efuse ?? string.Empty;
            CbSetFuses.IsChecked = p.setFuses;
            TxtLock.Text = p.lockBits ?? string.Empty;
            CbSetLock.IsChecked = p.setLock;
            TxtAdditional.Text = p.additional ?? string.Empty;
            CbVerbosity.SelectedItem = (int)p.verbosity;

            // USBasp bit-clock recovery
            if (p.programmer == "usbasp" &&
                double.TryParse(p.bitclock, NumberStyles.Float | NumberStyles.AllowThousands,
                                CultureInfo.InvariantCulture, out var us) && us > 0)
            {
                var freq = (int)(1 / (us * 0.000001));
                var match = Avrdude.USBaspFreqs.FirstOrDefault(s => s.freq > 0 && freq >= s.freq - 1);
                if (match != null) CbUsbAspFreq.SelectedItem = match;
            }
        }
        finally
        {
            _suppressChangeEvents = false;
        }
        RegenerateCmdLine();
    }

    private void SelectByProgrammerId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        foreach (var p in CbProgrammer.ItemsSource!.OfType<Programmer>())
            if (p.id == id) { CbProgrammer.SelectedItem = p; return; }
    }

    private void SelectByMcuId(string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        foreach (var m in CbMcu.ItemsSource!.OfType<MCU>())
            if (m.id == id) { CbMcu.SelectedItem = m; return; }
    }

    private static void SetFileFormat(ComboBox cb, string id)
    {
        var f = Avrdude.fileFormats.Find(x => x.Id == id);
        if (f != null) cb.SelectedItem = f;
    }

    private void SetFlashOp(string op)
    {
        _flashOp = op ?? FileOp.Write;
        RbFlashRead.IsChecked   = _flashOp == FileOp.Read;
        RbFlashVerify.IsChecked = _flashOp == FileOp.Verify;
        RbFlashWrite.IsChecked  = _flashOp == FileOp.Write;
    }

    private void SetEepromOp(string op)
    {
        _eepromOp = op ?? FileOp.Write;
        RbEepromRead.IsChecked   = _eepromOp == FileOp.Read;
        RbEepromVerify.IsChecked = _eepromOp == FileOp.Verify;
        RbEepromWrite.IsChecked  = _eepromOp == FileOp.Write;
    }

    private void OnPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded) return;
        if (CbPresets.SelectedItem is PresetData p)
            LoadPresetData(p);
    }

    // ---------- Action buttons ----------

    private void OnProgramClick(object sender, RoutedEventArgs e)
    {
        var cmd = _cmdLine.generate();
        if (string.IsNullOrWhiteSpace(cmd)) return;
        Task.Run(() => _avrdude.launch(cmd));
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        if (_avrdude != null && _avrdude.kill())
        {
            Util.consoleWriteLine();
            Util.consoleWriteLine("_AVRDUDEKILLED", Color.Red);
        }
    }

    private void OnDetectClick(object sender, RoutedEventArgs e)
    {
        // avrdude signature read using the current programmer/port
        if (_avrdude == null) return;
        Task.Run(() => _avrdude.detectMCU(_cmdLine.genReadSig()));
    }

    private void OnDetectedMcu(object sender, DetectedMCUEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var sig = e.signature;
            if (string.IsNullOrEmpty(sig))
            {
                Util.consoleWarning("_DETECTFAIL");
                return;
            }
            var m = _avrdude.mcus.Find(s => s.signature == sig && !s.IgnoreOnDetect);
            if (m != null)
            {
                Util.consoleWriteLine("_DETECTSUCCESS", m.signature, m.desc);
                SelectByMcuId(m.id);
            }
            else Util.consoleError("_UNKNOWNSIG", sig);
        });
    }

    private void OnReadFuseLock(object sender, ReadFuseLockEventArgs e)
    {
        var fuse = "0x" + (e.value ?? "").ToUpper().Replace("0X", "").PadLeft(2, '0');
        Dispatcher.UIThread.Post(() =>
        {
            switch (e.type)
            {
                case Avrdude.FuseLockType.Hfuse: TxtHFuse.Text = fuse; Util.consoleSuccess("_READHFUSE"); break;
                case Avrdude.FuseLockType.Lfuse: TxtLFuse.Text = fuse; Util.consoleSuccess("_READLFUSE"); break;
                case Avrdude.FuseLockType.Efuse: TxtEFuse.Text = fuse; Util.consoleSuccess("_READEFUSE"); break;
                case Avrdude.FuseLockType.Fuse:  TxtLFuse.Text = fuse; Util.consoleSuccess("_READFUSE"); break;
                case Avrdude.FuseLockType.Lock:  TxtLock.Text  = fuse; Util.consoleSuccess("_READLOCKBITS"); break;
                default:
                    Util.consoleError("_FUSELOCKREADFAIL");
                    Util.consoleWriteLine();
                    Util.consoleWriteLine(_avrdude.log ?? string.Empty);
                    break;
            }
        });
    }

    private void OnReadFusesClick(object sender, RoutedEventArgs e)
    {
        var m = _settings?.mcu;
        if (m == null) return;

        var types = new List<Avrdude.FuseLockType>();
        if (m.memoryTypes.Contains("hfuse")) types.Add(Avrdude.FuseLockType.Hfuse);
        if (m.memoryTypes.Contains("lfuse")) types.Add(Avrdude.FuseLockType.Lfuse);
        if (m.memoryTypes.Contains("efuse")) types.Add(Avrdude.FuseLockType.Efuse);
        if (m.memoryTypes.Contains("fuse"))  types.Add(Avrdude.FuseLockType.Fuse);

        if (types.Count == 0)
        {
            Util.consoleError("_NOSUPPORTEDFUSES", m.desc);
            return;
        }

        Util.consoleWriteLine("_READINGFUSES");
        var cmd = _cmdLine.generateReadFusesLock(types.ToArray());
        Task.Run(() => _avrdude.readFusesLock(cmd, types.ToArray()));
    }

    private void OnReadLockClick(object sender, RoutedEventArgs e)
    {
        Util.consoleWriteLine("_READINGLOCKBITS");
        var types = new[] { Avrdude.FuseLockType.Lock };
        var cmd = _cmdLine.generateReadFusesLock(types);
        Task.Run(() => _avrdude.readFusesLock(cmd, types));
    }

    private void OnWriteFusesClick(object sender, RoutedEventArgs e)
    {
        Util.consoleWriteLine("_WRITINGINGFUSES");
        var cmd = _cmdLine.generateWriteFuses();
        Task.Run(() => _avrdude.launch(cmd));
    }

    private void OnWriteLockClick(object sender, RoutedEventArgs e)
    {
        Util.consoleWriteLine("_WRITINGINGLOCKBITS");
        Task.Run(() => _avrdude.launch(_cmdLine.generateWriteLock()));
    }

    private void OnFlashGoClick(object sender, RoutedEventArgs e) =>
        Task.Run(() => _avrdude.launch(_cmdLine.generateFlash()));

    private void OnEepromGoClick(object sender, RoutedEventArgs e) =>
        Task.Run(() => _avrdude.launch(_cmdLine.generateEEPROM()));

    // ---------- File pickers ----------

    private async void OnBrowseFlashClick(object sender, RoutedEventArgs e) =>
        await BrowseFile(TxtFlashFile, "flash", _flashOp);

    private async void OnBrowseEepromClick(object sender, RoutedEventArgs e) =>
        await BrowseFile(TxtEepromFile, "EEPROM", _eepromOp);

    private async Task BrowseFile(TextBox target, string kind, string op)
    {
        var hexFilter = new FilePickerFileType("Intel HEX") { Patterns = new[] { "*.hex" } };
        var eepFilter = new FilePickerFileType("EEPROM") { Patterns = new[] { "*.eep" } };

        // For reads we're saving output; everything else is opening an existing file.
        if (op == FileOp.Read)
        {
            var save = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = $"Save {kind} to…",
                SuggestedFileName = kind == "flash" ? "flash.hex" : "eeprom.eep",
                FileTypeChoices = new[] { hexFilter, eepFilter, FilePickerFileTypes.All }
            });
            if (save is not null) target.Text = save.TryGetLocalPath();
        }
        else
        {
            var open = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = $"Open {kind} file…",
                AllowMultiple = false,
                FileTypeFilter = new[] { hexFilter, eepFilter, FilePickerFileTypes.All }
            });
            if (open.Count > 0) target.Text = open[0].TryGetLocalPath();
        }
    }

    // ---------- Fuse selector ----------

    private async void OnFuseSelectorClick(object sender, RoutedEventArgs e)
    {
        var m = _settings?.mcu;
        if (m == null) return;

        // Fuse fields into raw hex (strip 0x)
        var fuses = new[]
        {
            (TxtLFuse.Text ?? "").ToLower().Replace("0x", ""),
            (TxtHFuse.Text ?? "").ToLower().Replace("0x", ""),
            (TxtEFuse.Text ?? "").ToLower().Replace("0x", ""),
            (TxtLock.Text  ?? "").ToLower().Replace("0x", "")
        };

        var selector = new FuseSelectorWindow { WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var newFuses = await selector.EditFuseAndLocksAsync(this, m, fuses);
        if (newFuses == null) return;

        TxtLFuse.Text = "0x" + newFuses[0];
        TxtHFuse.Text = "0x" + newFuses[1];
        TxtEFuse.Text = "0x" + newFuses[2];
        TxtLock.Text  = "0x" + newFuses[3];
    }

    // ---------- Options / Preset manager / About ----------

    private async void OnOptionsClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OptionsWindow(_avrdude.programmers, _avrdude.mcus)
        {
            toolTips = Config.Prop.toolTips,
            usePreviousSettings = Config.Prop.usePreviousSettings,
            checkForUpdates = Config.Prop.checkForUpdates,
            avrdudeLocation = Config.Prop.avrdudeLoc,
            avrdudeConfLocation = Config.Prop.avrdudeConfLoc,
            avrSizeLocation = Config.Prop.avrSizeLoc,
            language = Config.Prop.language,
            hiddenProgrammers = Config.Prop.hiddenProgrammers,
            hiddenMCUs = Config.Prop.hiddenMCUs
        };
        await dlg.ShowDialog(this);
        if (!dlg.ConfirmedOk) return;

        var changedAvrdude = Config.Prop.avrdudeLoc != dlg.avrdudeLocation
                          || Config.Prop.avrdudeConfLoc != dlg.avrdudeConfLocation;
        var changedAvrSize = Config.Prop.avrSizeLoc != dlg.avrSizeLocation;

        Config.Prop.toolTips = dlg.toolTips;
        Config.Prop.usePreviousSettings = dlg.usePreviousSettings;
        Config.Prop.checkForUpdates = dlg.checkForUpdates;
        Config.Prop.avrdudeLoc = dlg.avrdudeLocation;
        Config.Prop.avrdudeConfLoc = dlg.avrdudeConfLocation;
        Config.Prop.avrSizeLoc = dlg.avrSizeLocation;
        Config.Prop.language = dlg.language;
        Config.Prop.hiddenProgrammers = dlg.hiddenProgrammers;
        Config.Prop.hiddenMCUs = dlg.hiddenMCUs;
        Config.Save();

        if (changedAvrdude)
        {
            await Task.Run(() => _avrdude.load());
            PopulateProgrammersAndMcus();
            UpdateVersionLabel();
        }
        if (changedAvrSize)
        {
            await Task.Run(() => _avrsize.load());
            _flashFile?.updateSize();
            _eepromFile?.updateSize();
        }
    }

    private async void OnPresetManagerClick(object sender, RoutedEventArgs e)
    {
        var dlg = new PresetManagerWindow
        {
            presets = _presets,
            currentSettings = MakePresetData(""),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };
        await dlg.ShowDialog(this);
    }

    private async void OnAboutClick(object sender, RoutedEventArgs e) =>
        await new AboutWindow().ShowDialog(this);
}
