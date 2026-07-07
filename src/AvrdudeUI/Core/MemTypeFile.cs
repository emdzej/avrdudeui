// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2014-2024, Zak Kemble. GNU GPL v3.

using System;

namespace AvrdudeUI.Core
{
    // Was tightly coupled to a WinForms TextBox in the original. Here it owns the
    // string state and fires SizeChanged; the UI binds a text box to Location.
    public class MemTypeFile
    {
        private readonly Avrsize avrsize;
        private string _location = string.Empty;

        public int size { get; private set; }
        public event EventHandler sizeChanged;

        public string location
        {
            get => _location;
            set
            {
                var next = value ?? string.Empty;
                if (next == _location) return;
                _location = next;
                updateSize();
            }
        }

        public MemTypeFile(Avrsize avrsize)
        {
            this.avrsize = avrsize;
            size = Avrsize.INVALID;
        }

        public void updateSize()
        {
            int newSize = avrsize.getSize(_location);
            if (newSize != size)
            {
                size = newSize;
                sizeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
