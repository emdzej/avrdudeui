// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2018-2024, Zak Kemble. GNU GPL v3.

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace AvrdudeUI.Core
{
    [XmlRoot("languages")]
    public class LanguagesMeta
    {
        public struct SupportedEntry
        {
            [XmlAttribute] public string name;
            [XmlAttribute] public string ename;
            [XmlAttribute] public string file;
        }

        [XmlArray("supported")]
        [XmlArrayItem("x")]
        public List<SupportedEntry> Supported = new List<SupportedEntry>();

#if DEBUG
        public struct KeyEntry
        {
            [XmlAttribute] public string name;
        }

        [XmlArray("keys")]
        [XmlArrayItem("k")]
        public List<KeyEntry> Expectedkeys = new List<KeyEntry>();
#endif
    }

    [XmlRoot("translation")]
    public class TranslationData
    {
        public struct TranslationEntry
        {
            [XmlAttribute] public string name;
            [XmlText] public string str;
        }

        [XmlArray("data")]
        [XmlArrayItem("string")]
        public List<TranslationEntry> Translations = new List<TranslationEntry>();
    }

    public class Language
    {
        private const string FILE_META = "_meta.xml";

        public static readonly Language Translation = new Language();

        private readonly Dictionary<string, string> languages = new Dictionary<string, string>();
        private readonly Dictionary<string, string> translations = new Dictionary<string, string>();
#if DEBUG
        private readonly HashSetD<string> expectedKeys = new HashSetD<string>();
#endif

        public Dictionary<string, string> Languages => languages;

        public string this[string key] => get(key);

        private Language() { }

        private void LoadMeta(string langsDir)
        {
            var metaFile = Path.Combine(langsDir, FILE_META);
            var metaData = new XmlFile<LanguagesMeta>(metaFile, isFullPath: true).Read();
            metaData.Supported.ForEach(t => languages.Add(t.file, $"{t.name} ({t.ename})"));
#if DEBUG
            metaData.Expectedkeys.ForEach(t => expectedKeys.Add(t.name));
#endif
        }

        private void LoadLanguage(string langsDir, string language)
        {
            var langFile = Path.Combine(langsDir, language + ".xml");
            var langData = new XmlFile<TranslationData>(langFile, isFullPath: true).Read();
            langData.Translations.ForEach(t =>
            {
                if (translations.ContainsKey(t.name))
                    throw new Exception($"Duplicate translation key: {t.name}");
                translations.Add(t.name, t.str);
            });
        }

        public void Load()
        {
            var langsDir = Path.Combine(AssemblyData.directory, "Languages");
            try
            {
                LoadMeta(langsDir);
                LoadLanguage(langsDir, Config.Prop.language);
            }
            catch (Exception ex)
            {
                MsgBox.error($"Error loading languages:{Environment.NewLine}{ex.Message}");
            }

#if DEBUG
            Debug(langsDir);
#endif
        }

        public string get(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;

            string str;
            if (!key.StartsWith("_")) // Only lookup translations for keys that start with an underscore
                str = key;
            else if (!translations.TryGetValue(key.Remove(0, 1), out str))
                str = key;

            return str;
        }

#if DEBUG
        private void Debug(string langsDir)
        {
            foreach (var l in languages)
            {
                var dbgTranslations = new Dictionary<string, string>();
                var file = $"{l.Key}.xml";

                Util.consoleWarning($"{file}: Checking...");

                var langFile = Path.Combine(langsDir, file);
                var langData = new XmlFile<TranslationData>(langFile, isFullPath: true).Read();
                langData.Translations.ForEach(t =>
                {
                    if (dbgTranslations.ContainsKey(t.name))
                        Util.consoleError($"{file}: Duplicate translation key: {t.name}");
                    else
                        dbgTranslations.Add(t.name, t.str);
                });

                foreach (var k in expectedKeys.Keys)
                {
                    if (!dbgTranslations.ContainsKey(k))
                        Util.consoleError($"{file}: Missing translation key: {k}");
                }

                foreach (var k in dbgTranslations.Keys)
                {
                    if (!expectedKeys.Contains(k))
                        Util.consoleError($"{file}: Unexpected translation key: {k}");
                }
            }
        }
#endif
    }
}
