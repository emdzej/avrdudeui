// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2024, Zak Kemble. GNU GPL v3.

using System.Collections;
using System.Collections.Generic;

namespace AvrdudeUI.Core
{
    // Retained from the original AVRDUDESS codebase; still used because its XML
    // serialization shape is baked into existing config.xml/presets.xml files.
    public class HashSetD<T> : IEnumerable<T>
    {
        private readonly Dictionary<T, bool> dict = new Dictionary<T, bool>();

        public Dictionary<T, bool>.KeyCollection Keys => dict.Keys;

        public HashSetD() { }

        public void Add(T item) { dict[item] = true; }

        public void AddRange(List<T> items)
        {
            items.ForEach(x =>
            {
                try { Add(x); }
                catch { }
            });
        }

        public void Clear() => dict.Clear();
        public bool Contains(T item) => dict.ContainsKey(item);
        public IEnumerator<T> GetEnumerator() => dict.Keys.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
