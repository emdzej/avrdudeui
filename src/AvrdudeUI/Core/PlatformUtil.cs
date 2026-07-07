// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AvrdudeUI.Core
{
    public static class PlatformUtil
    {
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsMac     => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsLinux   => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

        // Legacy shim — matches Util.isWindows() from AVRDUDESS
        public static bool isWindows() => IsWindows;

        public static void OpenUrl(string url)
        {
            try
            {
                if (IsMac)
                    Process.Start(new ProcessStartInfo("open", url) { UseShellExecute = false });
                else if (IsLinux)
                    Process.Start(new ProcessStartInfo("xdg-open", url) { UseShellExecute = false });
                else
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                AppMsgBox.Error($"Failed to open URL: {ex.Message}: {url}");
            }
        }

        public static long UnixTimeStamp() => UnixTimeStamp(DateTime.UtcNow);
        public static long UnixTimeStamp(DateTime dateTime) =>
            (long)dateTime.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

        public static string GetDescription(this Enum value)
        {
            var type = value.GetType();
            var name = Enum.GetName(type, value);
            if (name == null) return null;
            var field = type.GetField(name);
            if (field == null) return null;
            var attr = Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute)) as DescriptionAttribute;
            return attr?.Description;
        }

        public static string FileSizeFormat(int value)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            float len = value;
            int order = 0;
            while (len >= 1024 && order + 1 < sizes.Length)
            {
                order++;
                len /= 1024;
            }
            return $"{(int)len} {sizes[order]}";
        }
    }

    public static class AssemblyData
    {
        public static readonly Assembly Assembly = Assembly.GetEntryAssembly() ?? typeof(AssemblyData).Assembly;

        public static readonly string Title =
            (Attribute.GetCustomAttribute(Assembly, typeof(AssemblyTitleAttribute), false)
                as AssemblyTitleAttribute)?.Title ?? "AvrdudeUI";

        public static readonly string Copyright =
            (Attribute.GetCustomAttribute(Assembly, typeof(AssemblyCopyrightAttribute), false)
                as AssemblyCopyrightAttribute)?.Copyright ?? string.Empty;

        public static readonly Version Version = Assembly.GetName().Version ?? new Version(0, 0);

        // Directory containing the running assembly (falls back to AppContext.BaseDirectory)
        public static readonly string Directory =
            Path.GetDirectoryName(Assembly.Location) ?? AppContext.BaseDirectory;

        // Lowercase aliases matching the original AVRDUDESS API (used across Core)
        public static string title => Title;
        public static string copyright => Copyright;
        public static Version version => Version;
        public static string directory => Directory;

        // Returns the platform-native per-user data directory for this app.
        // macOS:   ~/Library/Application Support/AvrdudeUI
        // Linux:   $XDG_CONFIG_HOME or ~/.config/AvrdudeUI
        // Windows: %APPDATA%/AvrdudeUI
        public static string AppDataDir
        {
            get
            {
                if (PlatformUtil.IsMac)
                {
                    var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    return Path.Combine(home, "Library", "Application Support", Title);
                }

                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                return Path.Combine(appData, Title);
            }
        }
    }
}
