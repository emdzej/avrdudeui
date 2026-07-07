// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System;
using System.IO;
using System.Xml.Serialization;

namespace AvrdudeUI.Core
{
    public static class Config
    {
        private const string FILE_CONFIG = "config.xml";
        private static readonly XmlFile<ConfigData> xmlFile = new XmlFile<ConfigData>(FILE_CONFIG);
        public static ConfigData Prop;

        public static void Save()
        {
            Prop.configVersion = ConfigData.CONFIG_VERSION;
            try
            {
                xmlFile.Write(Prop);
            }
            catch (Exception ex)
            {
                MsgBox.error("_XMLWRITEERROR", "configuration", ex.Message);
            }
        }

        public static void Load()
        {
            try
            {
                Prop = xmlFile.Read();
            }
            catch (FileNotFoundException) { /* first run, expected */ }
            catch (DirectoryNotFoundException) { /* first run on macOS/Linux, expected */ }
            catch (Exception ex)
            {
                MsgBox.error($"An error occurred trying to load configuration:{Environment.NewLine}{ex.Message}");
            }

            if (Prop == null)
                Prop = new ConfigData();

            if (Prop.configVersion == 0)
            {
                // Probably first run or failed load — accept defaults.
            }
            else if (Prop.configVersion > ConfigData.CONFIG_VERSION)
            {
                MsgBox.warning($"Configuration file version ({Prop.configVersion}) is newer than expected ({ConfigData.CONFIG_VERSION}), things might not work properly...");
            }
        }
    }

    public class ConfigData
    {
        // Bump when a field is renamed or removed
        public const uint CONFIG_VERSION = 1;

        // System.Version isn't XML-serializable so use a plain struct
        public struct SkipVersion
        {
            public int Major;
            public int Minor;
        }

        public uint configVersion;
        public long updateCheck;

        [XmlElement(ElementName = "skipVersion")]
        public SkipVersion _skipVersion;

        public bool toolTips;
        public string avrdudeLoc;
        public string avrdudeConfLoc;
        public string avrSizeLoc;
        public WindowPoint windowLocation;
        public string language;
        public HashSetD<string> hiddenMCUs;
        public HashSetD<string> hiddenProgrammers;
        public PresetData previousSettings;
        public bool usePreviousSettings;
        public WindowSize windowSize;
        public bool checkForUpdates;

        [XmlIgnore]
        public Version skipVersion
        {
            get => new Version(_skipVersion.Major, _skipVersion.Minor);
            set
            {
                _skipVersion.Major = value.Major;
                _skipVersion.Minor = value.Minor;
            }
        }

        public ConfigData()
        {
            configVersion = 0;
            updateCheck = 0;
            _skipVersion.Major = 0;
            _skipVersion.Minor = 0;
            toolTips = true;
            avrdudeLoc = "";
            avrdudeConfLoc = "";
            avrSizeLoc = "";
            language = "english";
            hiddenMCUs = new HashSetD<string>();
            hiddenProgrammers = new HashSetD<string>();
            previousSettings = new PresetData();
            usePreviousSettings = true;
            checkForUpdates = true;
        }
    }

    // Serializable stand-ins for System.Drawing.Point / Size (used to be WinForms types).
    // Kept as structs so existing config.xml layouts stay compatible.
    public struct WindowPoint { public int X; public int Y; }
    public struct WindowSize  { public int Width; public int Height; }
}
