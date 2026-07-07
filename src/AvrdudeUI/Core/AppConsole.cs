// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System;
using System.Drawing;

namespace AvrdudeUI.Core
{
    // Abstraction over the console/log surface. The UI layer registers a Sink
    // that appends text to a scrollback control on the UI thread. Core code writes
    // through the static helpers below (Util.consoleWrite in the original AVRDUDESS
    // codebase — kept as `Util` for source compatibility).
    public interface IConsoleSink
    {
        void Write(string text, Color color);
        void Clear();
    }

    public static class AppConsole
    {
        private static IConsoleSink _sink;

        public static void SetSink(IConsoleSink sink) => _sink = sink;

        public static void Write(string text) => Write(text, Color.White);
        public static void Write(string text, Color color) => _sink?.Write(text ?? string.Empty, color);
        public static void Clear() => _sink?.Clear();
    }

    // Source-compatibility shim so ported code keeps using Util.consoleWrite etc.
    public static class Util
    {
        public static void consoleSet(IConsoleSink sink) => AppConsole.SetSink(sink);

        public static void consoleError(string text, params object[] args)
        {
            text = Language.Translation.get(text);
            text = string.Format(text, args);
            AppConsole.Write($"{Language.Translation.get("_ERRORUC")}: {text}{Environment.NewLine}", Color.Red);
        }

        public static void consoleWarning(string text, params object[] args)
        {
            text = Language.Translation.get(text);
            text = string.Format(text, args);
            AppConsole.Write($"{Language.Translation.get("_WARNINGUC")}: {text}{Environment.NewLine}", Color.Yellow);
        }

        public static void consoleSuccess(string text, params object[] args)
        {
            text = Language.Translation.get(text);
            text = string.Format(text, args);
            AppConsole.Write($"{Language.Translation.get("_SUCCESSUC")}: {text}{Environment.NewLine}", Color.LightGreen);
        }

        public static void consoleWriteLine() =>
            AppConsole.Write(Environment.NewLine, Color.White);

        public static void consoleWriteLine(string text, params object[] args) =>
            consoleWriteLine(text, Color.White, args);

        public static void consoleWriteLine(string text, Color color, params object[] args)
        {
            text = Language.Translation.get(text);
            text = string.Format(text, args);
            AppConsole.Write(text + Environment.NewLine, color);
        }

        public static void consoleWrite(string text) => AppConsole.Write(text, Color.White);
        public static void consoleWrite(string text, Color color) => AppConsole.Write(text, color);
        public static void consoleClear() => AppConsole.Clear();

        public static string fileSizeFormat(int value) => PlatformUtil.FileSizeFormat(value);
        public static void openURL(string url) => PlatformUtil.OpenUrl(url);
        public static bool isWindows() => PlatformUtil.IsWindows;
        public static long UnixTimeStamp() => PlatformUtil.UnixTimeStamp();
        public static long UnixTimeStamp(DateTime dt) => PlatformUtil.UnixTimeStamp(dt);
    }
}
