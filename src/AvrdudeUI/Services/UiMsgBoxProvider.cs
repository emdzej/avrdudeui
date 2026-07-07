using System;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using AvrdudeUI.Core;
using AvrdudeUI.Views;

namespace AvrdudeUI.Services;

// Shows a modal MessageDialog owned by whichever Window is the currently-active one.
//
// Threading rules:
//   • Error / Warning / Info notifications are fire-and-forget from the caller's
//     perspective. We must NEVER call ShowDialog(...).GetAwaiter().GetResult() on
//     the UI thread — that self-deadlocks the dispatcher and leaves the window
//     with 0×0 bounds (Avalonia's compositor can't paint while the loop is blocked).
//   • Confirm dialogs need a synchronous result. We only allow those to be called
//     from background threads (Dispatcher.UIThread.Invoke marshals + blocks the
//     caller, but the UI thread itself keeps pumping). If a confirm is called
//     *from* the UI thread, it degrades to a fire-and-forget non-modal show and
//     returns Cancel — safer than freezing.
public sealed class UiMsgBoxProvider : IMsgBoxProvider
{
    public void ShowError(string title, string message)   => FireAndForget(title, message, MessageDialog.Kind.Error);
    public void ShowWarning(string title, string message) => FireAndForget(title, message, MessageDialog.Kind.Warning);
    public void ShowInfo(string title, string message)    => FireAndForget(title, message, MessageDialog.Kind.Info);

    public MsgBoxResult ShowConfirm(string title, string message)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            // Called from the UI thread — can't block on a modal dialog without freezing
            // the app. Fall back to a non-modal display and treat as Cancel.
            ShowNonModal(title, message, MessageDialog.Kind.Confirm);
            return MsgBoxResult.Cancel;
        }

        bool ok = Dispatcher.UIThread.Invoke(() =>
        {
            var dlg = new MessageDialog(title, message, MessageDialog.Kind.Confirm);
            var owner = GetActiveWindow();
            if (owner is not null)
                dlg.ShowDialog(owner).GetAwaiter().GetResult();
            else
                dlg.Show();
            return dlg.ConfirmedOk;
        });
        return ok ? MsgBoxResult.Ok : MsgBoxResult.Cancel;
    }

    private static void FireAndForget(string title, string message, MessageDialog.Kind kind)
    {
        if (Dispatcher.UIThread.CheckAccess())
            ShowNonModal(title, message, kind);
        else
            Dispatcher.UIThread.Post(() => ShowNonModal(title, message, kind));
    }

    private static void ShowNonModal(string title, string message, MessageDialog.Kind kind)
    {
        var dlg = new MessageDialog(title, message, kind);
        var owner = GetActiveWindow();
        if (owner is not null)
            dlg.Show(owner); // non-modal, owned so it stays above and closes with the main window
        else
            dlg.Show();
    }

    private static Window GetActiveWindow()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime d)
        {
            foreach (var w in d.Windows)
                if (w.IsActive) return w;
            return d.MainWindow;
        }
        return null;
    }
}
