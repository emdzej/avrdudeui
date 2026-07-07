// AvrdudeUI — macOS port of AVRDUDESS
// Original: Copyright (C) 2013-2024, Zak Kemble. GNU GPL v3.

using System;

namespace AvrdudeUI.Core
{
    public enum MsgBoxResult { Ok, Cancel }

    public interface IMsgBoxProvider
    {
        void ShowError(string title, string message);
        void ShowWarning(string title, string message);
        void ShowInfo(string title, string message);
        MsgBoxResult ShowConfirm(string title, string message);
    }

    // Message-box abstraction. Core code writes through the static AppMsgBox / MsgBox facade;
    // the UI layer registers an implementation that pops a real Avalonia dialog.
    // Before any provider is registered (headless tests, early startup), messages are logged.
    public static class AppMsgBox
    {
        private static IMsgBoxProvider _provider;

        public static void SetProvider(IMsgBoxProvider provider) => _provider = provider;

        private static string Localize(string s, params object[] args)
        {
            s = Language.Translation.get(s);
            return string.Format(s, args);
        }

        public static void Error(string msg, params object[] args)
        {
            var text = Localize(msg, args);
            var title = Language.Translation.get("_ERROR");
            if (_provider != null) _provider.ShowError(title, text);
            else AppConsole.Write($"ERROR: {text}{Environment.NewLine}", System.Drawing.Color.Red);
        }

        public static void Warning(string msg, params object[] args)
        {
            var text = Localize(msg, args);
            var title = Language.Translation.get("_WARNING");
            if (_provider != null) _provider.ShowWarning(title, text);
            else AppConsole.Write($"WARNING: {text}{Environment.NewLine}", System.Drawing.Color.Yellow);
        }

        public static void Notice(string msg, params object[] args)
        {
            var text = Localize(msg, args);
            var title = Language.Translation.get("_NOTICE");
            if (_provider != null) _provider.ShowInfo(title, text);
            else AppConsole.Write($"{text}{Environment.NewLine}");
        }

        public static MsgBoxResult Confirm(string msg, params object[] args)
        {
            var text = Localize(msg, args);
            var title = Language.Translation.get("_CONFIRM");
            return _provider?.ShowConfirm(title, text) ?? MsgBoxResult.Cancel;
        }
    }

    // Source-compat facade so ported files keep calling MsgBox.error / MsgBox.confirm etc.
    public static class MsgBox
    {
        public static void error(string msg, params object[] args) => AppMsgBox.Error(msg, args);
        public static void warning(string msg, params object[] args) => AppMsgBox.Warning(msg, args);
        public static void notice(string msg, params object[] args) => AppMsgBox.Notice(msg, args);
        public static MsgBoxResult confirm(string msg, params object[] args) => AppMsgBox.Confirm(msg, args);
    }
}
