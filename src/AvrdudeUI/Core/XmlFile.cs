// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System.IO;
using System.Xml.Serialization;

namespace AvrdudeUI.Core
{
    public class XmlFile<T>
    {
        public string FilePath { get; private set; }
        private readonly XmlSerializer serializer = new XmlSerializer(typeof(T));

        public XmlFile(string fileName, bool isFullPath = false)
        {
            if (isFullPath) // For importing/exporting presets
                FilePath = fileName;
            else if (Portable.IsPortable) // Use app directory in portable mode
                FilePath = Path.Combine(AssemblyData.directory, fileName);
            else
                FilePath = Path.Combine(AssemblyData.AppDataDir, fileName);
        }

        public void Write(T obj)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            using var tw = new StreamWriter(FilePath, false);
            serializer.Serialize(tw, obj);
        }

        public T Read()
        {
            using var tr = new StreamReader(FilePath);
            return (T)serializer.Deserialize(tr);
        }
    }
}
