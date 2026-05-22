using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using MAXsCursor.Interop;

namespace MAXsCursor.Overlay;

// Layered window for the presentation big cursor: a filled disk with a small transparent
// centre hole and a high-contrast border. Same pipeline as NativeCursorWindow:
// DIB-backed, drawn once per appearance change, moved each mouse-move via SetWindowPos.
internal sealed class BigCursorWindow : IDisposable
{
    private const string ClassName = "MAXsCursorBigCursor_v1";

    private static readonly Win32.WndProc s_wndProc = StaticWndProc;
    private static ushort s_classAtom;
    private static readonly object s_classLock = new();

    private readonly int _sizePx;
    private nint _hwnd;
    private nint _memDC;
    private nint _dib;
    private nint _oldDibInDc;
    private nint _pBits;
    private bool _disposed;

    public nint Handle => _hwnd;

    public BigCursorWindow(int sizeDip, double dpiScale)
    {
        _sizePx = Math.Max(64, (int)Math.Round(sizeDip * dpiScale));
        EnsureClassRegistered();
        CreateNativeWindow();
        CreateDibSurface();
    }

    public void SetVisible(bool visible)
    {
        Win32.ShowWindow(_hwnd, visible ? WindowStyles.SW_SHOWNOACTIVATE : WindowStyles.SW_HIDE);
        if (visible)
        {
            const uint flags = WindowStyles.SWP_NOMOVE | WindowStyles.SWP_NOSIZE
                             | WindowStyles.SWP_NOACTIVATE | WindowStyles.SWP_SHOWWINDOW;
            Win32.SetWindowPos(_hwnd, WindowStyles.HWND_TOPMOST, 0, 0, 0, 0, flags);
        }
    }

    public void FollowCursor(int screenPxX, int screenPxY)
    {
        var half = _sizePx / 2;
        const uint flags = WindowStyles.SWP_NOSIZE | WindowStyles.SWP_NOACTIVATE | WindowStyles.SWP_NOZORDER;
        Win32.SetWindowPos(_hwnd, nint.Zero, screenPxX - half, screenPxY - half, 0, 0, flags);
    }

    // Rebuild the bitmap. Rare event (settings change). dpiScale converts dip sizes to px.
    public void ApplyAppearance(
        byte fillR, byte fillG, byte fillB,
        byte borderR, byte borderG, byte borderB,
        double sizeDip, double holeDip, double borderDip, double opacity, double dpiScale)
    {
        var alpha = (byte)Math.Round(255 * Math.Clamp(opacity, 0.0, 1.0));
        var outerR = Math.Max(4.0, sizeDip * dpiScale / 2.0);
        var innerR = Math.Clamp(holeDip * dpiScale / 2.0, 0.0, outerR - 2.0);
        var borderPx = Math.Max(1.0, borderDip * dpiScale);
        DrawCursor(alpha, fillR, fillG, fillB, borderR, borderG, borderB, outerR, innerR, borderPx);
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
            throw new InvalidOperationException("CreateWindowEx failed for BigCursorWindow");
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

    private unsafe void DrawCursor(
        byte alpha, byte fr, byte fg, byte fb, byte br, byte bg, byte bb,
        double outerR, double innerR, double borderPx)
    {
        var span = new Span<byte>((void*)_pBits, _sizePx * _sizePx * 4);
        span.Clear();

        using (var gfx = Graphics.FromHdc(_memDC))
        {
            gfx.SmoothingMode = SmoothingMode.AntiAlias;
            gfx.CompositingMode = CompositingMode.SourceOver;
            gfx.CompositingQuality = CompositingQuality.HighQuality;

            var c = _sizePx / 2.0f;
            var outer = new RectangleF((float)(c - outerR), (float)(c - outerR), (float)(outerR * 2), (float)(outerR * 2));
            var inner = new RectangleF((float)(c - innerR), (float)(c - innerR), (float)(innerR * 2), (float)(innerR * 2));

            // Donut fill: outer ellipse minus inner ellipse via alternate fill mode.
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(outer);
                if (innerR > 0.5) path.AddEllipse(inner);
                using var fill = new SolidBrush(Color.FromArgb(alpha, fr, fg, fb));
                gfx.FillPath(fill, path);
            }

            // Contrast border on both edges.
            using var pen = new Pen(Color.FromArgb(alpha, br, bg, bb), (float)borderPx);
            gfx.DrawEllipse(pen, outer);
            if (innerR > 0.5) gfx.DrawEllipse(pen, inner);
        }

        // Premultiply alpha in place (required by AC_SRC_ALPHA).
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
