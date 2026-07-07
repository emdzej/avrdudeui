// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System.Collections.Generic;

namespace AvrdudeUI.Core
{
    public class MCU : Part
    {
        private int _flash;
        private int _eeprom;
        private string _signature;
        private readonly List<string> _memoryTypes;

        public int flash
        {
            get => (_flash != -1) ? _flash : ((MCU)parent)?.flash ?? 0;
            private set => _flash = value;
        }

        public int eeprom
        {
            get => (_eeprom != -1) ? _eeprom : ((MCU)parent)?.eeprom ?? 0;
            private set => _eeprom = value;
        }

        public string signature
        {
            get => _signature ?? ((MCU)parent)?.signature ?? "?";
            private set => _signature = value;
        }

        public bool hide => ignore || Config.Prop.hiddenMCUs.Contains(id);

        // ATA661xx parts share signatures with the standalone MCU
        // (see original issue https://github.com/ZakKemble/AVRDUDESS/issues/81).
        public bool IgnoreOnDetect => id.StartsWith("ata661") || id.StartsWith("a661");

        public List<string> memoryTypes
        {
            get
            {
                var allTypes = new List<string>();
                allTypes.AddRange(_memoryTypes);
                if (parent != null)
                    allTypes.AddRange(((MCU)parent).memoryTypes);
                return allTypes;
            }
        }

        public MCU(string id, string desc = null, string signature = null, int flash = 0, int eeprom = 0,
                   MCU parent = null, List<string> memoryTypes = null)
            : base(id, desc, parent)
        {
            this.signature = signature?.ToLower();
            this.flash = flash;
            this.eeprom = eeprom;
            _memoryTypes = memoryTypes ?? new List<string>();
        }
    }
}
