// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System.Text;

namespace AvrdudeUI.Core
{
    // Decoupled from the WinForms Form1 in the original codebase — reads from a
    // plain AvrdudeSettings DTO so the UI layer can populate it however it likes.
    // NOTE: -u and -C args are added in Avrdude.launch()
    public class CmdLine
    {
        private readonly AvrdudeSettings s;
        private readonly StringBuilder sb = new StringBuilder();

        public CmdLine(AvrdudeSettings settings)
        {
            s = settings;
        }

        private void generateMain(bool addMCU = true)
        {
            sb.Clear();

            if (s.prog?.id.Length > 0)
                cmdLineOption("c", s.prog.id);

            if (s.mcu?.id.Length > 0 && addMCU)
                cmdLineOption("p", s.mcu.id);

            if (s.port.Length > 0)
                cmdLineOption("P", s.port);

            if (s.baudRate.Length > 0)
                cmdLineOption("b", s.baudRate);

            if (s.bitClock.Length > 0)
                cmdLineOption("B", s.bitClock);

            if (s.force)
                cmdLineOption("F");

            for (byte i = 0; i < s.verbosity; i++)
                cmdLineOption("v");
        }

        public string genReadSig()
        {
            generateMain(false);

            if (s.additionalSettings.Length > 0)
                sb.Append(s.additionalSettings + " ");

            // AVRDUDE requires -p even to read signatures — default to ATmega8
            cmdLineOption("p", "m8");

            return sb.ToString();
        }

        public string generateReadFusesLock(Avrdude.FuseLockType[] types)
        {
            generateMain();

            if (s.additionalSettings.Length > 0)
                sb.Append(s.additionalSettings + " ");

            for (int i = 0; i < types.Length; i++)
                cmdLineOption("U", $"{types[i].GetDescription()}:r:-:h");

            return sb.ToString();
        }

        public string generateWriteFuses()
        {
            generateMain();

            if (s.additionalSettings.Length > 0)
                sb.Append(s.additionalSettings + " ");

            addWriteFuses();

            return sb.ToString();
        }

        public string generateWriteLock()
        {
            generateMain();

            if (s.additionalSettings.Length > 0)
                sb.Append($"{s.additionalSettings} ");

            makeWriteFuseLock(Avrdude.FuseLockType.Lock, s.lockSetting);

            return sb.ToString();
        }

        public string generateFlash()
        {
            generateMain();

            if (s.disableVerify) cmdLineOption("V");
            if (s.disableFlashErase) cmdLineOption("D");
            if (s.eraseFlashAndEEPROM) cmdLineOption("e");

            if (s.additionalSettings.Length > 0)
                sb.Append($"{s.additionalSettings} ");

            if (s.flashFile.Length > 0)
                cmdLineOption("U", $"flash:{s.flashFileOperation}:\"{s.flashFile}\":{s.flashFileFormat}");

            return sb.ToString();
        }

        public string generateEEPROM()
        {
            generateMain();

            if (s.disableVerify) cmdLineOption("V");
            if (s.disableFlashErase) cmdLineOption("D");
            if (s.eraseFlashAndEEPROM) cmdLineOption("e");

            if (s.additionalSettings.Length > 0)
                sb.Append($"{s.additionalSettings} ");

            if (s.EEPROMFile.Length > 0)
                cmdLineOption("U", $"eeprom:{s.EEPROMFileOperation}:\"{s.EEPROMFile}\":{s.EEPROMFileFormat}");

            return sb.ToString();
        }

        public string generate()
        {
            generateMain();

            if (s.disableVerify) cmdLineOption("V");
            if (s.disableFlashErase) cmdLineOption("D");
            if (s.eraseFlashAndEEPROM) cmdLineOption("e");
            if (s.doNotWrite) cmdLineOption("n");

            if (s.additionalSettings.Length > 0)
                sb.Append($"{s.additionalSettings} ");

            if (s.flashFile.Length > 0)
                cmdLineOption("U", $"flash:{s.flashFileOperation}:\"{s.flashFile}\":{s.flashFileFormat}");

            if (s.EEPROMFile.Length > 0)
                cmdLineOption("U", $"eeprom:{s.EEPROMFileOperation}:\"{s.EEPROMFile}\":{s.EEPROMFileFormat}");

            if (s.setFuses)
                addWriteFuses();

            if (s.setLock)
                makeWriteFuseLock(Avrdude.FuseLockType.Lock, s.lockSetting);

            return sb.ToString();
        }

        private void makeWriteFuseLock(Avrdude.FuseLockType fuseLockType, string value)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length > 0)
                cmdLineOption("U", $"{fuseLockType.GetDescription()}:w:{value}:m");
        }

        private void addWriteFuses()
        {
            MCU mcu = s.mcu;
            if (mcu != null)
            {
                if (mcu.memoryTypes.Contains("lfuse")) makeWriteFuseLock(Avrdude.FuseLockType.Lfuse, s.lowFuse);
                if (mcu.memoryTypes.Contains("hfuse")) makeWriteFuseLock(Avrdude.FuseLockType.Hfuse, s.highFuse);
                if (mcu.memoryTypes.Contains("efuse")) makeWriteFuseLock(Avrdude.FuseLockType.Efuse, s.exFuse);
                if (mcu.memoryTypes.Contains("fuse"))  makeWriteFuseLock(Avrdude.FuseLockType.Fuse,  s.lowFuse);
            }
        }

        private void cmdLineOption(string arg, string val) => sb.Append($"-{arg} {val} ");
        private void cmdLineOption(string arg) => sb.Append($"-{arg} ");
    }
}
