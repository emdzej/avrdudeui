// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

namespace AvrdudeUI.Core
{
    public class FileFormat
    {
        public string Id { get; private set; }
        public string Desc { get; private set; }

        public FileFormat(string id, string desc)
        {
            Id = id;
            Desc = desc;
        }

        public void ApplyTranslation()
        {
            Desc = Language.Translation.get(Desc);
        }
    }
}
