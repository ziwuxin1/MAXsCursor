using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using MAXsCursor.Interop;

namespace MAXsCursor.Overlay;

internal partial class HudWindow : Window
{
    private nint _hwnd;
    private HwndSource? _hwndSource;
    private readonly KeyboardHudLayer _hud;

    // Custom position override (physical pixels). When enabled, HUD no longer
    // auto-tracks the cursor's monitor; user has pinned it to a fixed spot.
    private bool _customPos;
    private int _customX;
    private int _customY;

    public nint Handle => _hwnd;
    public event Action? InputWake;

    public HudWindow()
    {
        InitializeComponent();
        _hud = new KeyboardHudLayer();
        _hud.Margin = new Thickness(16);
        RootGrid.Children.Add(_hud);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var helper = new WindowInteropHelper(this);
        _hwnd = helper.Handle;

        ApplyExtendedStyles();
        ForceTopmostNoActivate();
        ContentRendered += (_, _) => RepositionToCursorMonitor();
        SizeChanged += (_, _) => RepositionToCursorMonitor();

        _hwndSource = HwndSource.FromHwnd(_hwnd);
        _hwndSource?.AddHook(WndProc);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == Win32.WM_APP_INPUT_WAKE)
        {
            InputWake?.Invoke();
            handled = true;
        }
        return nint.Zero;
    }

    private void ApplyExtendedStyles()
    {
        var current = Win32.GetWindowLongPtr(_hwnd, WindowStyles.GWL_EXSTYLE).ToInt64();
        var target = current
            | WindowStyles.WS_EX_LAYERED
            | WindowStyles.WS_EX_TRANSPARENT
            | WindowStyles.WS_EX_NOACTIVATE
            | WindowStyles.WS_EX_TOPMOST
            | WindowStyles.WS_EX_TOOLWINDOW;
        Win32.SetWindowLongPtr(_hwnd, WindowStyles.GWL_EXSTYLE, new nint((long)target));
    }

    private void ForceTopmostNoActivate()
    {
        const uint flags = WindowStyles.SWP_NOMOVE | WindowStyles.SWP_NOSIZE
                         | WindowStyles.SWP_NOACTIVATE | WindowStyles.SWP_SHOWWINDOW;
        Win32.SetWindowPos(_hwnd, WindowStyles.HWND_TOPMOST, 0, 0, 0, 0, flags);
    }

    // Relocate HUD to the monitor that currently contains the cursor. Called on each
    // size change and on each PushKeyChip, so recording sessions with multiple displays
    // get the HUD overlay on whichever display the user is actually working on.
    public void ApplyCustomPosition(bool custom, int x, int y)
    {
        _customPos = custom;
        _customX = x;
        _customY = y;
        RepositionToCursorMonitor();
    }

    public void RepositionToCursorMonitor()
    {
        if (_hwnd == nint.Zero) return;

        const uint flags = WindowStyles.SWP_NOSIZE | WindowStyles.SWP_NOACTIVATE | WindowStyles.SWP_NOZORDER;

        if (_customPos)
        {
            // User pinned the HUD. Skip auto-follow and respect the saved coordinates.
            Win32.SetWindowPos(_hwnd, nint.Zero, _customX, _customY, 0, 0, flags);
            return;
        }

        if (!Win32.GetCursorPos(out var pt)) return;

        var hmon = Win32.MonitorFromPoint(pt, Win32.MONITOR_DEFAULTTONEAREST);
        if (hmon == nint.Zero) return;

        var mi = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };
        if (!Win32.GetMonitorInfo(hmon, ref mi)) return;

        if (!Win32.GetWindowRect(_hwnd, out var wrect)) return;

        var winW = wrect.Right - wrect.Left;
        var winH = wrect.Bottom - wrect.Top;
        const int marginBottom = 80;

        // Horizontally centered on the cursor's monitor, anchored near the bottom.
        var x = mi.rcWork.Left + ((mi.rcWork.Right - mi.rcWork.Left) - winW) / 2;
        var y = mi.rcWork.Bottom - winH - marginBottom;

        Win32.SetWindowPos(_hwnd, nint.Zero, x, y, 0, 0, flags);
    }

    public void SetHudVisible(bool visible)
    {
        if (visible)
        {
            Win32.ShowWindow(_hwnd, WindowStyles.SW_SHOWNOACTIVATE);
            ForceTopmostNoActivate();
        }
        else
        {
            Win32.ShowWindow(_hwnd, WindowStyles.SW_HIDE);
        }
    }

    public void PushKeyChip(string text) => _hud.PushKey(text);
    public void TickHud() => _hud.Tick();
    public void ClearHud() => _hud.Clear();
    public void ApplyFontSize(double size) => _hud.SetFontSize(size);
}
