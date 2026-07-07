// AvrdudeUI — macOS port of AVRDUDESS
// Serial-port enumeration. Windows uses COMx names via System.IO.Ports; macOS/Linux
// expose them as character devices under /dev. avrdude expects the full device path.

using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;

namespace AvrdudeUI.Core
{
    public static class SerialPortEnumerator
    {
        // Prefixes we consider "USB serial adapters worth showing." macOS creates
        // both /dev/tty.* (blocks on open until DCD) and /dev/cu.* (non-blocking) for
        // each device — avrdude wants /dev/cu.* so that's what we surface.
        private static readonly string[] MacPrefixes =
        {
            "cu.usbmodem",   // CDC-ACM (Arduino Uno rev3+, Teensy, etc.)
            "cu.usbserial",  // FTDI, CH340
            "cu.SLAB_",      // Silicon Labs CP210x
            "cu.wchusbserial",
            "cu.Bluetooth"
        };

        // Match /dev/ttyUSB* + /dev/ttyACM* on Linux.
        private static readonly string[] LinuxPrefixes = { "ttyUSB", "ttyACM" };

        public static List<string> List()
        {
            if (PlatformUtil.IsMac) return EnumerateDev("/dev", MacPrefixes);
            if (PlatformUtil.IsLinux) return EnumerateDev("/dev", LinuxPrefixes);

            // Windows fallback — SerialPort.GetPortNames works there.
            try { return SerialPort.GetPortNames().OrderBy(n => n).ToList(); }
            catch { return new List<string>(); }
        }

        private static List<string> EnumerateDev(string dir, string[] prefixes)
        {
            var results = new List<string>();
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(dir))
                {
                    var name = Path.GetFileName(entry);
                    foreach (var p in prefixes)
                    {
                        if (name.StartsWith(p))
                        {
                            results.Add(entry);
                            break;
                        }
                    }
                }
            }
            catch
            {
                // /dev not readable → return whatever we have.
            }

            results.Sort();
            return results;
        }
    }
}
