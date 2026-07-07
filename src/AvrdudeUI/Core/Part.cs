// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

namespace AvrdudeUI.Core
{
    public class Part
    {
        public string id { get; private set; }
        private string _desc;
        protected Part parent;
        public bool ignore { get; private set; }

        public string desc
        {
            get => _desc ?? parent?.desc ?? "?";
            private set => _desc = value;
        }

        public Part(string id, string desc, Part parent)
        {
            this.id = id;
            this.desc = desc;
            this.parent = parent;

            ignore = id.StartsWith(".") || (desc?.ToLower().StartsWith("deprecated") ?? false);
        }
    }
}
