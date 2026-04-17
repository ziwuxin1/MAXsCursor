using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using MAXsCursor.Interop;

namespace MAXsCursor.Zoom;

internal static class ScreenCapture
{
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(nint hObject);

    // Captures the full bounds of the monitor that currently contains the cursor.
    // Returns the captured bitmap plus the monitor rectangle in physical pixels so the
    // zoom window can size/position itself to cover the same monitor precisely.
    public static (BitmapSource image, int left, int top, int width, int height) CaptureCursorMonitor()
    {
        Win32.GetCursorPos(out var pt);
        var hmon = Win32.MonitorFromPoint(pt, Win32.MONITOR_DEFAULTTONEAREST);
        var mi = new Win32.MONITORINFO { cbSize = (uint)Marshal.SizeOf<Win32.MONITORINFO>() };

        int left, top, width, height;
        if (hmon != nint.Zero && Win32.GetMonitorInfo(hmon, ref mi))
        {
            left = mi.rcMonitor.Left;
            top = mi.rcMonitor.Top;
            width = mi.rcMonitor.Right - mi.rcMonitor.Left;
            height = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
        }
        else
        {
            // Primary monitor fallback. SystemParameters returns DIPs; the native Win32 width
            // and height are what we want for BitBlt, so convert back via the transform.
            left = 0;
            top = 0;
            width = (int)SystemParameters.PrimaryScreenWidth;
            height = (int)SystemParameters.PrimaryScreenHeight;
        }

        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(left, top, 0, 0, new System.Drawing.Size(width, height), CopyPixelOperation.SourceCopy);
        }

        var hBitmap = bmp.GetHbitmap();
        try
        {
            var src = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, nint.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return (src, left, top, width, height);
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }
}
