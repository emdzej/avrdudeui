// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

namespace AvrdudeUI.Core
{
    // Plain DTO consumed by CmdLine to build avrdude argument strings.
    // The original AVRDUDESS read these directly off the Form1 fields; here the
    // UI populates one of these each time the command line needs regeneration.
    public class AvrdudeSettings
    {
        public Programmer prog;
        public MCU mcu;
        public string port = string.Empty;
        public string baudRate = string.Empty;
        public string bitClock = string.Empty;

        public bool force;
        public bool disableVerify;
        public bool disableFlashErase;
        public bool eraseFlashAndEEPROM;
        public bool doNotWrite;

        public string flashFile = string.Empty;
        public string flashFileFormat = "a";
        public string flashFileOperation = FileOp.Write;

        public string EEPROMFile = string.Empty;
        public string EEPROMFileFormat = "a";
        public string EEPROMFileOperation = FileOp.Write;

        public string lowFuse = string.Empty;
        public string highFuse = string.Empty;
        public string exFuse = string.Empty;
        public string lockSetting = string.Empty;
        public bool setFuses;
        public bool setLock;

        public string additionalSettings = string.Empty;
        public byte verbosity;
    }
}
