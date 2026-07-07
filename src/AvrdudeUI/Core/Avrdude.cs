// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace AvrdudeUI.Core
{
    public class DetectedMCUEventArgs : EventArgs
    {
        public string signature { get; set; }
        public DetectedMCUEventArgs(string s) { signature = s; }
    }

    public class ReadFuseLockEventArgs : EventArgs
    {
        public Avrdude.FuseLockType type { get; set; }
        public string value { get; set; }
        public ReadFuseLockEventArgs(Avrdude.FuseLockType t, string v) { type = t; value = v; }
    }

    public class Avrdude : Executable
    {
        public class UsbAspFreq
        {
            public string name { get; private set; }
            public string bitClock { get; private set; }
            public int freq { get; private set; }

            public UsbAspFreq(string name) { this.name = name; }

            public UsbAspFreq(string name, string bitClock, int freq)
            {
                this.name = name;
                this.bitClock = bitClock;
                this.freq = freq;
            }
        }

        private const string FILE_AVRDUDE = "avrdude";
        private const string FILE_AVRDUDECONF = "avrdude.conf";

        // Directories to probe for avrdude.conf on Unix-like systems. Order matters —
        // Homebrew is preferred so a `brew install avrdude` on Apple Silicon "just works".
        private static readonly string[] UnixConfDirs = new[]
        {
            "/opt/homebrew/etc",           // Apple Silicon Homebrew
            "/usr/local/etc",              // Intel Homebrew / manual install
            "/etc",
            "/opt/local/etc"               // MacPorts
        };

        public static readonly List<UsbAspFreq> USBaspFreqs = new List<UsbAspFreq>()
        {
            new UsbAspFreq("Default (375 KHz)"),
            new UsbAspFreq("1.5 MHz (0.66)",  "0.66",  1500000),
            new UsbAspFreq("750 KHz (1.33)",  "1.33",  750000),
            new UsbAspFreq("375 KHz (2.66)",  "2.66",  375000),
            new UsbAspFreq("187.5 KHz (5.33)","5.33",  187500),
            new UsbAspFreq("93.75 KHz (10.66)","10.66", 93750),
            new UsbAspFreq("32 KHz (31.25)",  "31.25", 32000),
            new UsbAspFreq("16 KHz (62.5)",   "62.5",  16000),
            new UsbAspFreq("8 KHz (125)",     "125",   8000),
            new UsbAspFreq("4 KHz (250)",     "250",   4000),
            new UsbAspFreq("2 KHz (500)",     "500",   2000),
            new UsbAspFreq("1 KHz (1000)",    "1000",  1000),
            new UsbAspFreq("500 Hz (2000)",   "2000",  500),
        };

        public static readonly List<FileFormat> fileFormats = new List<FileFormat>()
        {
            new FileFormat("a", "_FILEFMT_AUTO"),
            new FileFormat("i", "_FILEFMT_HEX"),
            new FileFormat("s", "_FILEFMT_SREC"),
            new FileFormat("r", "_FILEFMT_BIN"),
            new FileFormat("d", "_FILEFMT_DECR"),
            new FileFormat("h", "_FILEFMT_HEXR"),
            new FileFormat("b", "_FILEFMT_BINR")
        };

        public enum FuseLockType
        {
            [Description("")]      None,
            [Description("hfuse")] Hfuse,
            [Description("lfuse")] Lfuse,
            [Description("efuse")] Efuse,
            [Description("fuse")]  Fuse,
            [Description("lock")]  Lock
        }

        private enum ParseMemType { None, Flash, Eeprom, Unknown }

        private readonly List<Programmer> _programmers;
        private readonly List<MCU> _mcus;
        public string version { get; private set; }
        public event EventHandler OnVersionChange;
        public event EventHandler<DetectedMCUEventArgs> OnDetectedMCU;
        public event EventHandler<ReadFuseLockEventArgs> OnReadFuseLock;

        private static readonly Regex strArrSplitRegex = new Regex("\"[\\s]*,[\\s]*\"");

        public List<Programmer> programmers => _programmers;
        public List<MCU> mcus => _mcus;
        public string log => outputLogStdErr;

        public Avrdude()
        {
            _programmers = new List<Programmer>();
            _mcus = new List<MCU>();
            version = "";
        }

        public void load()
        {
            load(FILE_AVRDUDE, Config.Prop.avrdudeLoc);

            getVersion();

            _programmers.Clear();
            _mcus.Clear();

            loadConfig(Config.Prop.avrdudeConfLoc);

            _programmers.Sort((t1, t2) => t1.id.CompareTo(t2.id));
            _mcus.Sort((t1, t2) => t1.desc.CompareTo(t2.desc));
        }

        private void getVersion()
        {
            version = "";

            if (launch("", OutputTo.Memory))
            {
                waitForExit();

                if (outputLogStdErr != null)
                {
                    string log = outputLogStdErr;
                    int pos = log.IndexOf("avrdude version");
                    if (pos > -1)
                    {
                        log = log.Substring(pos);
                        var commaIdx = log.IndexOf(',');
                        if (commaIdx > 0)
                            version = log.Substring(0, commaIdx);
                    }
                }
            }

            OnVersionChange?.Invoke(this, EventArgs.Empty);
        }

        private void savePart(bool isProgrammer, string parentId, string id, string desc, string signature, int flash, int eeprom, List<string> memoryTypes)
        {
            if (id != null)
            {
                // AVRDUDE 7.2+ config entries may declare multiple comma-separated IDs.
                // Expand each into its own Part so users can find any alias in the dropdown.
                var ids = strArrSplitRegex.Split(id);
                if (ids.Length > 1)
                {
                    foreach (var subId in ids)
                        savePart(isProgrammer, parentId, subId, desc, signature, flash, eeprom, memoryTypes);
                    return;
                }

                if (isProgrammer)
                {
                    Programmer parent = (parentId != null) ? _programmers.Find(m => m.id == parentId) : null;
                    _programmers.Add(new Programmer(id, desc, parent));
                }
                else
                {
                    desc = desc.ToUpper().Replace("XMEGA", "xmega").Replace("MEGA", "mega").Replace("TINY", "tiny");
                    MCU parent = (parentId != null) ? _mcus.Find(m => m.id == parentId) : null;
                    _mcus.Add(new MCU(id, desc, signature, flash, eeprom, parent, memoryTypes));
                }
            }
        }

        private void loadConfig(string confLoc)
        {
            string conf_loc = null;

            if (!string.IsNullOrEmpty(confLoc))
            {
                conf_loc = confLoc;
            }
            else
            {
                // On Unix-likes (macOS + Linux) probe the well-known etc directories first.
                if (!PlatformUtil.IsWindows)
                {
                    foreach (var dir in UnixConfDirs)
                    {
                        var candidate = Path.Combine(dir, FILE_AVRDUDECONF);
                        if (File.Exists(candidate)) { conf_loc = candidate; break; }
                    }
                }

                if (conf_loc == null)
                {
                    conf_loc = Path.Combine(AssemblyData.directory, FILE_AVRDUDECONF);
                    if (!File.Exists(conf_loc))
                        conf_loc = Path.Combine(Directory.GetCurrentDirectory(), FILE_AVRDUDECONF);
                }
            }

            if (string.IsNullOrEmpty(conf_loc) || !File.Exists(conf_loc))
            {
                Util.consoleError("_AVRCONFMISSING", FILE_AVRDUDECONF);
                return;
            }

            var fileName = Path.GetFileName(conf_loc);

            if (new FileInfo(conf_loc).Length > 10 * 1024 * 1024)
            {
                Util.consoleError("_CONFIG_FILE_TOO_LARGE", fileName);
                return;
            }

            string[] lines;
            try
            {
                lines = File.ReadAllLines(conf_loc);
            }
            catch (Exception ex)
            {
                Util.consoleError("_AVRCONFREADERROR", fileName, ex.Message);
                return;
            }

            char[] trimChars = new char[3] { ' ', '"', ';' };

            string parentId = null;
            string id = null;
            string desc = null;
            string signature = null;
            int flash = -1;
            int eeprom = -1;
            List<string> memoryTypes = new List<string>();
            bool valid = false;

            ParseMemType memType = ParseMemType.None;
            bool isProgrammer = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i].Trim();

                bool lineProgrammer = s.StartsWith("programmer");
                bool linePart = s.StartsWith("part");

                if (lineProgrammer || linePart)
                {
                    parentId = null;
                    id = null;
                    desc = null;
                    signature = null;
                    flash = -1;
                    eeprom = -1;
                    memoryTypes = new List<string>();
                    memType = ParseMemType.None;
                    valid = true;

                    var parts = s.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 2 && parts[1].Trim(trimChars) == "parent")
                        parentId = parts[2].Trim(trimChars);

                    isProgrammer = lineProgrammer;
                }
                else if (s == ";")
                {
                    if (memType != ParseMemType.None)
                    {
                        memType = ParseMemType.None;
                    }
                    else
                    {
                        if (valid)
                            savePart(isProgrammer, parentId, id, desc, signature, flash, eeprom, memoryTypes);
                        valid = false;
                    }
                }
                else if (valid)
                {
                    int pos = s.IndexOf('=');
                    if (pos > 0)
                    {
                        string key = s.Substring(0, pos - 1).Trim();
                        string val = s.Substring(pos + 1).Trim(trimChars);

                        if (key == "id") id = val;
                        else if (key == "desc") desc = val;
                        else if (key == "signature")
                        {
                            signature = val;
                            signature = signature.Replace("0x", "").Replace(" ", "");
                        }
                        else if (key == "size" && memType != ParseMemType.None && memType != ParseMemType.Unknown)
                        {
                            int memTmp = 0;
                            if (!int.TryParse(val, out memTmp))
                            {
                                if (val.StartsWith("0x")) val = val.Substring(2);
                                int.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out memTmp);
                            }

                            if (memType == ParseMemType.Flash) flash = memTmp;
                            else if (memType == ParseMemType.Eeprom) eeprom = memTmp;
                        }
                    }
                    else if (s.StartsWith("memory"))
                    {
                        pos = s.IndexOf('"');
                        if (pos > -1)
                        {
                            string mem = s.Substring(pos - 1).Trim(trimChars).ToLower();
                            if (mem == "flash") memType = ParseMemType.Flash;
                            else if (mem == "eeprom") memType = ParseMemType.Eeprom;
                            else memType = ParseMemType.Unknown;

                            memoryTypes.Add(mem);
                        }
                    }
                }
            }

            if (_programmers.Count == 0 && _mcus.Count == 0)
                Util.consoleError("_NOTHING_FOUND_IN_CONFIG_FILE", fileName);
            else
                Util.consoleWriteLine("_CONFIG_LOADED_PROGS_MCUS", _programmers.FindAll(x => !x.ignore).Count, mcus.FindAll(x => !x.ignore).Count);
        }

        public new bool launch(string args, Action<object> onFinish, object param, OutputTo outputTo = OutputTo.Console)
        {
            if (args.Trim().Length > 0)
            {
                string confLoc = Config.Prop.avrdudeConfLoc;
                if (confLoc != "")
                    args = $"-C \"{confLoc}\" {args}";
            }

            if (outputTo == OutputTo.Console)
                Util.consoleWriteLine("~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~ ~");

            Util.consoleWriteLine($">>>: {Path.GetFileName(binary)} {args}", Color.Aquamarine);

            return base.launch(args, onFinish, param, outputTo);
        }

        public bool launch(string args, OutputTo outputTo = OutputTo.Console) => launch(args, null, null, outputTo);

        public void detectMCU(string args)
        {
            launch(args, (object _) =>
            {
                var detectedSignature = "";
                var sigIdx = outputLogStdErr.IndexOf("signature = ");

                if (sigIdx != -1 && outputLogStdErr.Length > sigIdx + 12 + 8)
                    detectedSignature = outputLogStdErr.Substring(sigIdx + 12, 8).Replace(" ", "").Replace("0x", "").ToLower();
                else
                {
                    // AVRDUDE v8.0 emits no signature on success — fall back to ATmega8 (see CmdLine.genReadSig)
                    if (!outputLogStdErr.ToLower().Contains("error"))
                        detectedSignature = "1e9307";
                }

                OnDetectedMCU?.Invoke(this, new DetectedMCUEventArgs(detectedSignature));
            }, null, OutputTo.Memory);
        }

        public void readFusesLock(string args, FuseLockType[] types)
        {
            launch(args, readFusesLockComplete, types, OutputTo.Memory);
        }

        private void readFusesLockComplete(object param)
        {
            FuseLockType[] types = param as FuseLockType[];
            if (types == null) return;

            string log = outputLogStdErr.ToLower();

            // avrdude 7.1+ prints USBasp firmware SCK-period warnings as errors — strip them
            // so the "read succeeded" path stays reachable (upstream issue #80).
            log = log.Replace("error: cannot set sck period", null);

            if (log.IndexOf("error") > -1 || log.IndexOf("fail") > -1)
            {
                OnReadFuseLock?.Invoke(this, new ReadFuseLockEventArgs(FuseLockType.None, ""));
                return;
            }

            string[] values = outputLogStdOut.Split(
                new[] { Environment.NewLine },
                StringSplitOptions.RemoveEmptyEntries
            );

            if (values.Length != types.Length)
            {
                OnReadFuseLock?.Invoke(this, new ReadFuseLockEventArgs(FuseLockType.None, ""));
                return;
            }

            for (int i = 0; i < types.Length; i++)
                OnReadFuseLock?.Invoke(this, new ReadFuseLockEventArgs(types[i], values[i].Trim()));
        }
    }
}
