// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System.Collections.Generic;

namespace AvrdudeUI.Core
{
    public class Programmer : Part
    {
        private string _type;

        private const string MCU_ISP = "m8";
        private const string MCU_JTAG = "m32";
        private const string MCU_TPI = "t10";
        private const string MCU_PDI = "";

        public static readonly Dictionary<string, List<string>> progInterfaces = new Dictionary<string, List<string>>()
        {
            { "avrftdi",      new List<string>() { MCU_ISP, MCU_JTAG } },
            { "buspirate",    new List<string>() { MCU_ISP } },
            { "buspirate_bb", new List<string>() { MCU_ISP, MCU_TPI } },
            { "usbasp",       new List<string>() { MCU_ISP, MCU_TPI } }
        };

        public string type
        {
            get => _type ?? ((Programmer)parent)?.type ?? "?";
            private set => _type = value;
        }

        public bool hide => ignore || Config.Prop.hiddenProgrammers.Contains(id);

        public Programmer(string id, string desc = null, Programmer parent = null)
            : base(id, desc, parent) { }

        public List<string> getInterfaces() => null;
    }
}
