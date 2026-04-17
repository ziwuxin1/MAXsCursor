using System.Windows.Interop;
using MAXsCursor.Interop;

namespace MAXsCursor.Core;

internal sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 0x5A01;
    private static readonly nint HWND_MESSAGE = new(-3);

    private readonly HwndSource _source;
    private readonly Action _onPressed;
    private bool _registered;
    private bool _disposed;

    public HotkeyManager(Action onPressed)
    {
        _onPressed = onPressed;

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

    public bool Register()
    {
        if (_registered) return true;

        _registered = Win32.RegisterHotKey(
            _source.Handle,
            HotkeyId,
            WindowStyles.MOD_ALT | WindowStyles.MOD_NOREPEAT,
            (uint)WindowStyles.VK_F5);

        return _registered;
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WindowStyles.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            _onPressed();
            handled = true;
        }
        return nint.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_registered)
        {
            Win32.UnregisterHotKey(_source.Handle, HotkeyId);
            _registered = false;
        }

        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
