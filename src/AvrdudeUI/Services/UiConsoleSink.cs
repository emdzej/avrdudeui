using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using AvrdudeUI.Core;

namespace AvrdudeUI.Services;

// Renders IConsoleSink output into a SelectableTextBlock's Inlines collection so
// each write can carry its own foreground color (matches the original AVRDUDESS
// RichTextBox behaviour). Marshals to the UI thread; Core code may write from any thread.
//
// Brushes are UI-thread affinity types in Avalonia — creating them on a background
// thread and referencing them later trips a VerifyAccess on the compositor.
// So we cache one brush per RGBA value and always allocate/hand out inside the
// UI-thread continuation.
public sealed class UiConsoleSink : IConsoleSink
{
    private readonly SelectableTextBlock _text;
    private readonly ScrollViewer _scroll;
    private readonly ConcurrentDictionary<uint, SolidColorBrush> _brushCache = new();

    public UiConsoleSink(SelectableTextBlock text, ScrollViewer scroll)
    {
        _text = text;
        _scroll = scroll;
        _text.Inlines ??= new InlineCollection();
    }

    public void Write(string text, System.Drawing.Color color)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Pack ARGB into a uint so the cache key is a struct.
        uint key = ((uint)color.A << 24) | ((uint)color.R << 16) | ((uint)color.G << 8) | color.B;

        void append()
        {
            var brush = _brushCache.GetOrAdd(key,
                _ => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B)));

            _text.Inlines!.Add(new Run(text) { Foreground = brush });
            if (text.Contains('\n'))
                _scroll.ScrollToEnd();
        }

        if (Dispatcher.UIThread.CheckAccess())
            append();
        else
            Dispatcher.UIThread.Post(append);
    }

    public void Clear()
    {
        void clear() => _text.Inlines?.Clear();

        if (Dispatcher.UIThread.CheckAccess())
            clear();
        else
            Dispatcher.UIThread.Post(clear);
    }
}
