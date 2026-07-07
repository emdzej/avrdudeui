// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2014-2024, Zak Kemble. GNU GPL v3.

using System;
using System.IO;

namespace AvrdudeUI.Core
{
    public class Avrsize : Executable
    {
        private const string FILE_AVR_SIZE = "avr-size";
        public const int INVALID = -1;

        public void load()
        {
            load(FILE_AVR_SIZE, Config.Prop.avrSizeLoc,
                 enableConsoleWrite: false,
                 optional: true,
                 installHint: "install with: brew tap osx-cross/avr && brew install avr-binutils");
        }

        // Get size of flash/EEPROM file
        public int getSize(string file)
        {
            int totalSize = INVALID;
            if (File.Exists(file) && launch($"\"{file}\"", null, null, OutputTo.Memory))
            {
                waitForExit();
                totalSize = parse();
            }
            return totalSize;
        }

        private int parse()
        {
            if (outputLogStdOut == null)
                return INVALID;

            var data = outputLogStdOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (data.Length < 2)
                return INVALID;

            data = data[1].Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            if (data.Length < 2)
                return INVALID;

            int.TryParse(data[0], out int textSize);
            int.TryParse(data[1], out int dataSize);
            return textSize + dataSize;
        }
    }
}
