using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using MAXsCursor.Interop;

namespace MAXsCursor.Overlay;

// One animated click ripple. Layered window sized once for the worst-case radius. While
// active, Advance() redraws an expanding fading ring and re-uploads via UpdateLayeredWindow.
// Lives on the UI thread, advanced from the RenderClock tick. Short lived (a few hundred ms).
internal sealed class ClickRippleWindow : IDisposable
{
    private const string ClassName = "MAXsCursorClickRipple_v1";

    private static readonly Win32.WndProc s_wndProc = StaticWndProc;
    private static ushort s_classAtom;
    private static readonly object s_classLock = new();

    private readonly int _sizePx;
    private readonly double _dpiScale;
    private readonly float _strokePx;
    private nint _hwnd;
    private nint _memDC;
    private nint _dib;
    private nint _oldDibInDc;
    private nint _pBits;
    private bool _disposed;

    private bool _active;
    private long _startTimestamp;
    private double _durationMs;
    private double _maxRadiusPx;
    private byte _r, _g, _b;

    public bool IsActive => _active;

    public ClickRippleWindow(double maxRadiusCeilingDip, double dpiScale)
    {
        _dpiScale = dpiScale;
        _strokePx = (float)Math.Max(2.0, 4.0 * dpiScale);
        // Window must fit the largest possible ring plus the stroke width and AA margin.
        _sizePx = (int)Math.Round(maxRadiusCeilingDip * dpiScale * 2 + _strokePx * 2 + 8);
        EnsureClassRegistered();
        CreateNativeWindow();
        CreateDibSurface();
    }

    // Begin a ripple centred at the given screen pixel point, in the given colour.
    public void Spawn(int screenPxX, int screenPxY, byte r, byte g, byte b, double maxRadiusDip, int durationMs)
    {
        _r = r; _g = g; _b = b;
        _maxRadiusPx = maxRadiusDip * _dpiScale;
        _durationMs = Math.Max(50, durationMs);
        _startTimestamp = Stopwatch.GetTimestamp();
        _active = true;

        var half = _sizePx / 2;
        const uint flags = WindowStyles.SWP_NOSIZE | WindowStyles.SWP_NOACTIVATE;
        Win32.SetWindowPos(_hwnd, WindowStyles.HWND_TOPMOST, screenPxX - half, screenPxY - half, 0, 0, flags);
        Win32.ShowWindow(_hwnd, WindowStyles.SW_SHOWNOACTIVATE);
        RenderFrame(0.0);
    }

    // Advance the animation. Returns true while still active, false once finished.
    public bool Advance(long now)
    {
        if (!_active) return false;
        var elapsedMs = (now - _startTimestamp) * 1000.0 / Stopwatch.Frequency;
        var progress = elapsedMs / _durationMs;
        if (progress >= 1.0)
        {
            Hide();
            return false;
        }
        RenderFrame(progress);
        return true;
    }

    public void Hide()
    {
        _active = false;
        Win32.ShowWindow(_hwnd, WindowStyles.SW_HIDE);
    }

    private void RenderFrame(double progress)
    {
        // Ease-out for the radius, linear fade for the alpha.
        var eased = 1.0 - (1.0 - progress) * (1.0 - progress);
        var radius = Math.Max(1.0, eased * _maxRadiusPx);
        var alpha = (byte)Math.Round(255 * Math.Clamp(1.0 - progress, 0.0, 1.0));
        DrawRing(alpha, radius);
        PresentBitmap();
    }

    private static void EnsureClassRegistered()
    {
        lock (s_classLock)
        {
            if (s_classAtom != 0) return;
            var wc = new Win32.WNDCLASSEX
            {
                cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
                style = 0,
                lpfnWndProc = s_wndProc,
                hInstance = Win32.GetModuleHandle(null),
                lpszClassName = ClassName
            };
            s_classAtom = Win32.RegisterClassEx(ref wc);
        }
    }

    private static nint StaticWndProc(nint hwnd, uint msg, nint wParam, nint lParam)
        => Win32.DefWindowProc(hwnd, msg, wParam, lParam);

    private void CreateNativeWindow()
    {
        var exStyle = WindowStyles.WS_EX_LAYERED
                    | WindowStyles.WS_EX_TRANSPARENT
                    | WindowStyles.WS_EX_NOACTIVATE
                    | WindowStyles.WS_EX_TOPMOST
                    | WindowStyles.WS_EX_TOOLWINDOW;
        var style = WindowStyles.WS_POPUP;

        _hwnd = Win32.CreateWindowEx(
            exStyle, ClassName, null, style,
            -9999, -9999, _sizePx, _sizePx,
            nint.Zero, nint.Zero, Win32.GetModuleHandle(null), nint.Zero);

        if (_hwnd == nint.Zero)
        {
            throw new InvalidOperationException("CreateWindowEx failed for ClickRippleWindow");
        }
    }

    private void CreateDibSurface()
    {
        var screenDC = Win32.GetDC(nint.Zero);
        try
        {
            _memDC = Win32.CreateCompatibleDC(screenDC);
            var bmi = new Win32.BITMAPINFOHEADER
            {
                biSize = (uint)Marshal.SizeOf<Win32.BITMAPINFOHEADER>(),
                biWidth = _sizePx,
                biHeight = -_sizePx,
                biPlanes = 1,
                biBitCount = 32,
                biCompression = Win32.BI_RGB
            };
            _dib = Win32.CreateDIBSection(screenDC, ref bmi, Win32.DIB_RGB_COLORS, out _pBits, nint.Zero, 0);
            _oldDibInDc = Win32.SelectObject(_memDC, _dib);
        }
        finally
        {
            Win32.ReleaseDC(nint.Zero, screenDC);
        }
    }

    private unsafe void DrawRing(byte alpha, double radius)
    {
        var span = new Span<byte>((void*)_pBits, _sizePx * _sizePx * 4);
        span.Clear();

        using (var gfx = Graphics.FromHdc(_memDC))
        {
            gfx.SmoothingMode = SmoothingMode.AntiAlias;
            gfx.CompositingMode = CompositingMode.SourceOver;
            gfx.CompositingQuality = CompositingQuality.HighQuality;

            var c = _sizePx / 2.0f;
            var rad = (float)radius;
            var bounds = new RectangleF(c - rad, c - rad, rad * 2, rad * 2);
            using var pen = new Pen(Color.FromArgb(alpha, _r, _g, _b), _strokePx);
            gfx.DrawEllipse(pen, bounds);
        }

        var p = (byte*)_pBits;
        var pixels = _sizePx * _sizePx;
        for (var i = 0; i < pixels; i++)
        {
            var o = i * 4;
            var a = p[o + 3];
            if (a == 0)
            {
                p[o] = 0; p[o + 1] = 0; p[o + 2] = 0;
            }
            else if (a < 255)
            {
                p[o] = (byte)(p[o] * a / 255);
                p[o + 1] = (byte)(p[o + 1] * a / 255);
                p[o + 2] = (byte)(p[o + 2] * a / 255);
            }
        }
    }

    private unsafe void PresentBitmap()
    {
        var screenDC = Win32.GetDC(nint.Zero);
        try
        {
            var size = new Win32.SIZE { cx = _sizePx, cy = _sizePx };
            var ptSrc = new Win32.POINT { X = 0, Y = 0 };
            var blend = new Win32.BLENDFUNCTION
            {
                BlendOp = Win32.AC_SRC_OVER,
                BlendFlags = 0,
                SourceConstantAlpha = 255,
                AlphaFormat = Win32.AC_SRC_ALPHA
            };
            Win32.UpdateLayeredWindow(_hwnd, screenDC, null, &size, _memDC, &ptSrc, 0, &blend, Win32.ULW_ALPHA);
        }
        finally
        {
            Win32.ReleaseDC(nint.Zero, screenDC);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _active = false;

        if (_hwnd != nint.Zero) { Win32.DestroyWindow(_hwnd); _hwnd = nint.Zero; }
        if (_memDC != nint.Zero)
        {
            if (_oldDibInDc != nint.Zero) Win32.SelectObject(_memDC, _oldDibInDc);
            Win32.DeleteDC(_memDC);
            _memDC = nint.Zero;
        }
        if (_dib != nint.Zero) { Win32.DeleteObject(_dib); _dib = nint.Zero; }
    }
}
