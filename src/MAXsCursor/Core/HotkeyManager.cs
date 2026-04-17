using System.Windows.Interop;
using MAXsCursor.Interop;

namespace MAXsCursor.Core;

// Host a message-only HwndSource on the UI thread, register any number of global
// hotkeys on it via RegisterHotKey, and dispatch each WM_HOTKEY to its callback
// using the hotkey id as the lookup key.
internal sealed class HotkeyManager : IDisposable
{
    private static readonly nint HWND_MESSAGE = new(-3);

    private readonly HwndSource _source;
    private readonly Dictionary<int, Action> _handlers = new();
    private int _nextId = 0x5A00;
    private bool _disposed;

    public HotkeyManager()
    {
        var parameters = new HwndSourceParameters("MAXsCursor.HotkeySink")
        {
            ParentWindow = HWND_MESSAGE,
            WindowStyle = 0,
            ExtendedWindowStyle = 0,
            Width = 0,
            Height = 0
        };

        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public int? Register(uint modifiers, uint vkCode, Action onPressed)
    {
        var id = _nextId++;
        var ok = Win32.RegisterHotKey(
            _source.Handle,
            id,
            modifiers | WindowStyles.MOD_NOREPEAT,
            vkCode);
        if (!ok) return null;
        _handlers[id] = onPressed;
        return id;
    }

    public void Unregister(int id)
    {
        if (_handlers.Remove(id))
        {
            Win32.UnregisterHotKey(_source.Handle, id);
        }
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WindowStyles.WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_handlers.TryGetValue(id, out var handler))
            {
                handler();
                handled = true;
            }
        }
        return nint.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var id in _handlers.Keys)
        {
            Win32.UnregisterHotKey(_source.Handle, id);
        }
        _handlers.Clear();

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
