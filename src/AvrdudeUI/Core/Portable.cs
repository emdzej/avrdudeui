// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2024, Zak Kemble. GNU GPL v3.

using System;
using System.IO;

namespace AvrdudeUI.Core
{
    public static class Portable
    {
        private const string FILE_PORTABLE = "portable.txt";
        private static bool hasReadFile;
        private static bool _isPortable;

        public static bool IsPortable
        {
            get
            {
                if (hasReadFile)
                    return _isPortable;

                hasReadFile = true;
                var path = Path.Combine(AssemblyData.directory, FILE_PORTABLE);
                try
                {
                    using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
                    var buffer = new byte[1];
                    if (fs.Read(buffer, 0, 1) == 1 && buffer[0] == 'Y')
                        _isPortable = true;
                }
                catch (Exception)
                {
                    // Missing / unreadable → non-portable mode
                }

                return _isPortable;
            }
        }
    }
}
