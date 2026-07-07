// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Xml.Serialization;

namespace AvrdudeUI.Core
{
    public class Presets
    {
        private const string FILE_PRESETS = "presets.xml";
        private readonly XmlFile<BindingList<PresetData>> xmlFile;
        private BindingList<PresetData> presetList = new BindingList<PresetData>();
        private readonly bool isImport = false;

        public BindingList<PresetData> BindingSource => presetList;

        public List<PresetData> Items => new List<PresetData>(presetList);

        public Presets()
        {
            xmlFile = new XmlFile<BindingList<PresetData>>(FILE_PRESETS);
        }

        public Presets(string file)
        {
            xmlFile = new XmlFile<BindingList<PresetData>>(file, true);
            isImport = true;
        }

        public void Add(PresetData preset)
        {
            presetList.Add(preset);
            BumpDefault();
        }

        public void Remove(PresetData preset)
        {
            presetList.Remove(preset);
            BumpDefault();
        }

        private void BumpDefault()
        {
            int idx = new List<PresetData>(presetList).FindIndex(s => s.name == "Default");
            if (idx > 0)
            {
                PresetData p = presetList[idx];
                presetList.RemoveAt(idx);
                presetList.Insert(0, p);
            }
        }

        public void Save()
        {
            try { xmlFile.Write(presetList); }
            catch (Exception ex) { MsgBox.error("_XMLWRITEERROR", "presets", ex.Message); }
        }

        public void Load()
        {
            try
            {
                presetList = xmlFile.Read();
            }
            catch (FileNotFoundException) when (!isImport) { /* first run, expected */ }
            catch (DirectoryNotFoundException) when (!isImport) { /* first run on macOS/Linux, expected */ }
            catch (Exception ex)
            {
                MsgBox.error($"An error occurred trying to load presets:{Environment.NewLine}{ex.Message}");
            }

            if (presetList == null)
            {
                presetList = new BindingList<PresetData>();
                if (!isImport)
                    Add(new PresetData("Default"));
            }
        }
    }

    [XmlType(TypeName = "Preset")] // Backwards-compat with <v2.0 presets.xml
    public class PresetData : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private string _name;

        public string name
        {
            get => _name;
            set { _name = value; NotifyPropertyChanged("name"); }
        }

        public string programmer = "";
        public string mcu = "";
        public string port;
        public string baud;
        public string bitclock;
        public string flashFile;
        public string flashFormat = "a";
        public string flashOp = FileOp.Write;
        public string EEPROMFile;
        public string EEPROMFormat = "a";
        public string EEPROMOp = FileOp.Write;
        public bool force;
        public bool disableVerify;
        public bool disableFlashErase;
        public bool eraseFlashAndEEPROM;
        public bool doNotWrite;
        public string lfuse;
        public string hfuse;
        public string efuse;
        public bool setFuses;
        public string lockBits;
        public bool setLock;
        public string additional;
        public byte verbosity;

        public PresetData() { }

        public PresetData(string name) { this.name = name; }

        public PresetData(PresetData source) { copyFrom(source); }

        public void copyFrom(PresetData source)
        {
            name = source.name;
            programmer = source.programmer;
            mcu = source.mcu;
            port = source.port;
            baud = source.baud;
            bitclock = source.bitclock;
            flashFile = source.flashFile;
            flashFormat = source.flashFormat;
            flashOp = source.flashOp;
            EEPROMFile = source.EEPROMFile;
            EEPROMFormat = source.EEPROMFormat;
            EEPROMOp = source.EEPROMOp;
            force = source.force;
            disableVerify = source.disableVerify;
            disableFlashErase = source.disableFlashErase;
            eraseFlashAndEEPROM = source.eraseFlashAndEEPROM;
            doNotWrite = source.doNotWrite;
            lfuse = source.lfuse;
            hfuse = source.hfuse;
            efuse = source.efuse;
            setFuses = source.setFuses;
            lockBits = source.lockBits;
            setLock = source.setLock;
            additional = source.additional;
            verbosity = source.verbosity;
        }
    }
}
