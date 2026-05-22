# Presentation Cursor Mode Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a toggleable teaching mode (default Alt+F6) that draws an enlarged high-contrast cursor (filled disk with a small centre hole) and shows a coloured ripple on each left/middle/right click.

**Architecture:** The big cursor reuses the proven `NativeCursorWindow` layered-window pipeline and follows the cursor on the hook thread (zero latency, same as the ring). Click ripples are short animated layered windows driven on the UI thread by the existing `RenderClock` tick, spawned from the click events already flowing through `EventBus`. Orchestration (toggle, hotkey, settings apply) lives in `App.xaml.cs`, consistent with how the ring and zoom are already wired.

**Tech Stack:** C# 12 / .NET 8 / WPF, Win32 layered windows (`UpdateLayeredWindow`), GDI+ for bitmap drawing, `System.Text.Json` for settings.

**Testing note:** Per CLAUDE.md this project is manual-test only, no unit tests. Each task ends with `dotnet build` (must succeed) and a commit. Behavioural verification happens in the final manual acceptance task.

**Design deviation from spec:** The spec listed a `PresentationCursorController.cs`. We omit it. The big cursor must run on the hook thread (owned by `HookManager`, like the ring) while ripples run on the UI thread, so a single cross-thread controller would be an awkward wrapper. Instead the two render pieces are self-contained (`BigCursorWindow`, `ClickRippleWindow`, `ClickRippleController`) and `App.xaml.cs` coordinates toggle state, matching the existing orchestration pattern.

**Build command (run from repo root):**
```
dotnet build src/MAXsCursor/MAXsCursor.csproj
```
Expected: `Build succeeded` with 0 errors.

---

## Task 1: Settings model fields

**Files:**
- Modify: `src/MAXsCursor/Settings/SettingsModel.cs`

- [ ] **Step 1: Add the persisted fields**

Insert these properties after the existing `ZoomHotkeyVk` property (before the `Language` property) in `SettingsModel`:

```csharp
    // --- Presentation cursor mode (enlarged high-contrast cursor + click ripple) ---
    // Runtime on/off is NOT persisted: the mode always starts off and is toggled by hotkey.
    // Only appearance and the hotkey binding persist.

    // Presentation toggle hotkey. Default Alt+F6 (VK_F6 = 0x75).
    public uint PresentationHotkeyMods { get; set; } = 0x0001; // Alt
    public uint PresentationHotkeyVk { get; set; } = 0x75;     // F6

    // Big cursor: a filled disk with a small transparent centre hole and a contrast border.
    public double BigCursorSize { get; set; } = 64.0;          // outer diameter, dip
    public double BigCursorHoleSize { get; set; } = 16.0;      // centre hole diameter, dip
    public double BigCursorBorderThickness { get; set; } = 3.0;// dip
    public string BigCursorColor { get; set; } = "#FFC400";    // fill, RRGGBB
    public string BigCursorBorderColor { get; set; } = "#FFFFFF"; // contrast border, RRGGBB
    public double BigCursorOpacity { get; set; } = 0.85;       // 0..1

    // Click ripple: expanding ring per click, distinct colour per button.
    public bool ClickRippleEnabled { get; set; } = true;
    public string LeftClickColor { get; set; } = "#FFE000";    // yellow
    public string MiddleClickColor { get; set; } = "#00E676";  // green
    public string RightClickColor { get; set; } = "#2979FF";   // blue
    public double RippleMaxRadius { get; set; } = 48.0;        // dip
    public int RippleDurationMs { get; set; } = 420;
```

- [ ] **Step 2: Extend `Clone()`**

In the `Clone()` method, add these assignments inside the `new()` initializer (after `ZoomHotkeyVk = ZoomHotkeyVk,` and before `Language = Language`):

```csharp
        PresentationHotkeyMods = PresentationHotkeyMods,
        PresentationHotkeyVk = PresentationHotkeyVk,
        BigCursorSize = BigCursorSize,
        BigCursorHoleSize = BigCursorHoleSize,
        BigCursorBorderThickness = BigCursorBorderThickness,
        BigCursorColor = BigCursorColor,
        BigCursorBorderColor = BigCursorBorderColor,
        BigCursorOpacity = BigCursorOpacity,
        ClickRippleEnabled = ClickRippleEnabled,
        LeftClickColor = LeftClickColor,
        MiddleClickColor = MiddleClickColor,
        RightClickColor = RightClickColor,
        RippleMaxRadius = RippleMaxRadius,
        RippleDurationMs = RippleDurationMs,
```

- [ ] **Step 3: Build**

Run: `dotnet build src/MAXsCursor/MAXsCursor.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```
git add src/MAXsCursor/Settings/SettingsModel.cs
git commit -m "feat: add presentation cursor settings fields"
```

---

## Task 2: Carry click position through EventBus

The ripple needs the screen coordinates of each click. `MouseButtonEvent` currently has no position. Add `X` and `Y`.

**Files:**
- Modify: `src/MAXsCursor/Core/EventBus.cs`
- Modify: `src/MAXsCursor/Core/HookManager.cs:178`

- [ ] **Step 1: Add X, Y to the event record**

In `EventBus.cs`, replace the `MouseButtonEvent` record definition:

```csharp
internal readonly record struct MouseButtonEvent(MouseButton Button, ModifierMask Modifiers, long TimestampTicks);
```

with:

```csharp
internal readonly record struct MouseButtonEvent(MouseButton Button, ModifierMask Modifiers, long TimestampTicks, int X, int Y);
```

- [ ] **Step 2: Populate X, Y at the enqueue site**

In `HookManager.cs`, in `MouseHookProc`, replace this line:

```csharp
                        s_bus?.EnqueueMouseButton(new MouseButtonEvent(button, mods, data.time));
```

with:

```csharp
                        s_bus?.EnqueueMouseButton(new MouseButtonEvent(button, mods, data.time, data.pt.X, data.pt.Y));
```

- [ ] **Step 3: Build**

Run: `dotnet build src/MAXsCursor/MAXsCursor.csproj`
Expected: Build succeeded. (The HUD drain in `App.xaml.cs` only reads `Button`/`Modifiers`, so it keeps compiling unchanged.)

- [ ] **Step 4: Commit**

```
git add src/MAXsCursor/Core/EventBus.cs src/MAXsCursor/Core/HookManager.cs
git commit -m "feat: carry click position in MouseButtonEvent"
```

---

## Task 3: BigCursorWindow

A layered window mirroring `NativeCursorWindow`, drawing a filled disk (annulus) with a small transparent centre hole and a contrast border. Static bitmap, moved by `SetWindowPos`.

**Files:**
- Create: `src/MAXsCursor/Overlay/BigCursorWindow.cs`

- [ ] **Step 1: Create the file**

```csharp
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
```

- [ ] **Step 2: Build**

Run: `dotnet build src/MAXsCursor/MAXsCursor.csproj`
Expected: Build succeeded. (The class is not referenced yet, that is fine.)

- [ ] **Step 3: Commit**

```
git add src/MAXsCursor/Overlay/BigCursorWindow.cs
git commit -m "feat: add BigCursorWindow layered renderer"
```

---

## Task 4: Hook-thread wiring for the big cursor

Make `HookManager` own a `BigCursorWindow`, move it on the same hook-thread mouse-move callback as the ring, and expose marshaled init/apply/visibility methods that mirror the existing ring methods.

**Files:**
- Modify: `src/MAXsCursor/Core/HookManager.cs`

- [ ] **Step 1: Add fields**

After this existing field block near the top of the class:

```csharp
    // Direct mouse-move handler invoked from inside the hook callback on the hook
    // thread. Set after the cursor window is created on the same thread.
    private static Action<int, int>? s_onMouseMove;
```

add:

```csharp
    // Second mouse-move handler for the presentation big cursor. Only invoked while the
    // big cursor is visible, so a hidden mode costs nothing in the hot path.
    private static Action<int, int>? s_onMouseMoveBig;
    private static volatile bool s_bigCursorVisible;
```

And after this instance field:

```csharp
    // Cursor window lives on the hook thread.
    private NativeCursorWindow? _cursor;
```

add:

```csharp
    // Big cursor window also lives on the hook thread, alongside the ring.
    private BigCursorWindow? _bigCursor;
```

- [ ] **Step 2: Move the big cursor in the mouse-move hot path**

In `MouseHookProc`, replace this block:

```csharp
                if (msg == Win32.WM_MOUSEMOVE)
                {
                    var handler = s_onMouseMove;
                    handler?.Invoke(data.pt.X, data.pt.Y);
                }
```

with:

```csharp
                if (msg == Win32.WM_MOUSEMOVE)
                {
                    var handler = s_onMouseMove;
                    handler?.Invoke(data.pt.X, data.pt.Y);
                    if (s_bigCursorVisible)
                    {
                        var bigHandler = s_onMouseMoveBig;
                        bigHandler?.Invoke(data.pt.X, data.pt.Y);
                    }
                }
```

- [ ] **Step 3: Add the public marshaled methods**

After the existing `SetCursorVisible` method:

```csharp
    public void SetCursorVisible(bool visible)
    {
        RunOnHookThread(() => _cursor?.SetVisible(visible));
    }
```

add:

```csharp
    // Create the big cursor on the hook thread but leave it hidden: presentation mode
    // starts off. Wires the move handler so it tracks the cursor once shown.
    public void InitializeBigCursor(int sizeDip, double dpiScale, Action<BigCursorWindow> configure)
    {
        RunOnHookThread(() =>
        {
            _bigCursor = new BigCursorWindow(sizeDip, dpiScale);
            configure(_bigCursor);
            s_onMouseMoveBig = _bigCursor.FollowCursor;
        });
    }

    public void ApplyBigCursor(
        byte fillR, byte fillG, byte fillB,
        byte borderR, byte borderG, byte borderB,
        double sizeDip, double holeDip, double borderDip, double opacity, double dpiScale)
    {
        RunOnHookThread(() => _bigCursor?.ApplyAppearance(
            fillR, fillG, fillB, borderR, borderG, borderB,
            sizeDip, holeDip, borderDip, opacity, dpiScale));
    }

    public void SetBigCursorVisible(bool visible)
    {
        RunOnHookThread(() =>
        {
            _bigCursor?.SetVisible(visible);
            s_bigCursorVisible = visible;
            // Snap to the current cursor position so it appears under the pointer immediately.
            if (visible && _bigCursor is not null && Win32.GetCursorPos(out var pt))
            {
                _bigCursor.FollowCursor(pt.X, pt.Y);
            }
        });
    }
```

- [ ] **Step 4: Tear down the big cursor on thread exit**

In `ThreadMain`'s `finally` block, replace:

```csharp
            s_onMouseMove = null;
            _cursor?.Dispose();
            _cursor = null;
```

with:

```csharp
            s_onMouseMove = null;
            s_onMouseMoveBig = null;
            s_bigCursorVisible = false;
            _cursor?.Dispose();
            _cursor = null;
            _bigCursor?.Dispose();
            _bigCursor = null;
```

- [ ] **Step 5: Build**

Run: `dotnet build src/MAXsCursor/MAXsCursor.csproj`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```
git add src/MAXsCursor/Core/HookManager.cs
git commit -m "feat: own and track big cursor on the hook thread"
```

---

## Task 5: ClickRippleWindow

A single animated ripple: an expanding, fading stroked ring. The bitmap is sized once for the worst-case radius (the settings slider maximum) and redrawn each frame while active.

**Files:**
- Create: `src/MAXsCursor/Overlay/ClickRippleWindow.cs`

- [ ] **Step 1: Create the file**

```csharp
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
```

- [ ] **Step 2: Build**

Run: `dotnet build src/MAXsCursor/MAXsCursor.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```
git add src/MAXsCursor/Overlay/ClickRippleWindow.cs
git commit -m "feat: add ClickRippleWindow animated renderer"
```

---

## Task 6: ClickRippleController

Owns a small fixed pool of ripple windows, spawns a ripple per click in the per-button colour, advances all active ripples each tick, and holds the current ripple settings.

**Files:**
- Create: `src/MAXsCursor/Overlay/ClickRippleController.cs`

- [ ] **Step 1: Create the file**

```csharp
using System.Diagnostics;
using MAXsCursor.Core;

namespace MAXsCursor.Overlay;

// Manages a fixed pool of ClickRippleWindow instances on the UI thread. Spawns a ripple
// at each left/middle/right click in the configured colour and advances them per frame.
internal sealed class ClickRippleController : IDisposable
{
    // Worst-case radius the bitmap must accommodate. Must be >= the settings slider maximum.
    public const double MaxRadiusCeilingDip = 160.0;
    private const int PoolSize = 4;

    private readonly ClickRippleWindow[] _pool;
    private bool _disposed;

    // Current settings snapshot.
    private bool _enabled = true;
    private double _maxRadiusDip = 48.0;
    private int _durationMs = 420;
    private (byte r, byte g, byte b) _left = (0xFF, 0xE0, 0x00);
    private (byte r, byte g, byte b) _middle = (0x00, 0xE6, 0x76);
    private (byte r, byte g, byte b) _right = (0x29, 0x79, 0xFF);

    public ClickRippleController(double dpiScale)
    {
        _pool = new ClickRippleWindow[PoolSize];
        for (var i = 0; i < PoolSize; i++)
        {
            _pool[i] = new ClickRippleWindow(MaxRadiusCeilingDip, dpiScale);
        }
    }

    public void ApplySettings(
        bool enabled, double maxRadiusDip, int durationMs,
        (byte r, byte g, byte b) left, (byte r, byte g, byte b) middle, (byte r, byte g, byte b) right)
    {
        _enabled = enabled;
        _maxRadiusDip = Math.Clamp(maxRadiusDip, 4.0, MaxRadiusCeilingDip);
        _durationMs = durationMs;
        _left = left;
        _middle = middle;
        _right = right;
    }

    // Spawn a ripple for a click. Ignores wheel, X1/X2, and disabled state.
    public void Spawn(MouseButton button, int screenPxX, int screenPxY)
    {
        if (!_enabled) return;

        (byte r, byte g, byte b) color;
        switch (button)
        {
            case MouseButton.Left: color = _left; break;
            case MouseButton.Middle: color = _middle; break;
            case MouseButton.Right: color = _right; break;
            default: return;
        }

        var slot = AcquireSlot();
        slot.Spawn(screenPxX, screenPxY, color.r, color.g, color.b, _maxRadiusDip, _durationMs);
    }

    // Pick a free window, or the first one if all are busy (overwrites the oldest visually).
    private ClickRippleWindow AcquireSlot()
    {
        for (var i = 0; i < _pool.Length; i++)
        {
            if (!_pool[i].IsActive) return _pool[i];
        }
        return _pool[0];
    }

    // Advance every active ripple. Cheap no-op when none are active.
    public void Tick()
    {
        var now = Stopwatch.GetTimestamp();
        for (var i = 0; i < _pool.Length; i++)
        {
            if (_pool[i].IsActive) _pool[i].Advance(now);
        }
    }

    // Hide all ripples immediately (mode turned off or master disable).
    public void Clear()
    {
        for (var i = 0; i < _pool.Length; i++)
        {
            if (_pool[i].IsActive) _pool[i].Hide();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        for (var i = 0; i < _pool.Length; i++)
        {
            _pool[i].Dispose();
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build src/MAXsCursor/MAXsCursor.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```
git add src/MAXsCursor/Overlay/ClickRippleController.cs
git commit -m "feat: add ClickRippleController pool and tick"
```

---

## Task 7: Localized strings

**Files:**
- Modify: `src/MAXsCursor/Settings/Strings.cs`

- [ ] **Step 1: Add the new section strings**

In `Strings.cs`, after the line `public static string KeyFontSize => _en ? "Key text size" : "按键文字大小";` add:

```csharp
    // Presentation cursor section
    public static string SectionPresentation => _en ? "Presentation cursor" : "演示光标";
    public static string PresBigSize => _en ? "Cursor size" : "光标大小";
    public static string PresHole => _en ? "Centre hole" : "中心孔";
    public static string PresBorder => _en ? "Border width" : "描边宽度";
    public static string PresColor => _en ? "Cursor color" : "光标颜色";
    public static string PresOpacity => _en ? "Cursor opacity" : "光标透明度";
    public static string RippleEnabled => _en ? "Click ripple" : "点击水波纹";
    public static string RippleLeftColor => _en ? "Left click color" : "左键颜色";
    public static string RippleMiddleColor => _en ? "Middle click color" : "中键颜色";
    public static string RippleRightColor => _en ? "Right click color" : "右键颜色";
    public static string RippleSize => _en ? "Ripple size" : "水波纹大小";
    public static string RippleDuration => _en ? "Ripple duration" : "水波纹时长";
```

- [ ] **Step 2: Add the shortcut row strings**

After the line `public static string ShortcutZoomHint => _en ? "Freeze the screen, zoom in, draw on top" : "冻结屏幕，放大后可以画图";` add:

```csharp
    public static string ShortcutPresentationLabel => _en ? "Presentation cursor" : "演示光标";
    public static string ShortcutPresentationHint => _en ? "Enlarged high-contrast cursor + click ripple" : "放大高对比光标 + 点击水波纹";
```

- [ ] **Step 3: Mention the mode in the help text**

In the `HelpBodyZh` const, insert this block immediately before the `【系统托盘】` block:

```
【演示光标】
• 默认快捷键 Alt+F6（可在 ""快捷键"" 区重新指定），按一下开启，再按关闭。
• 开启后鼠标处显示一个放大、高对比的大光标（中间留小孔，方便看清目标）。
• 左键 / 中键 / 右键点击时会冒出不同颜色的水波纹，方便观众看清点了哪里。
• 大小 / 颜色 / 透明度 / 水波纹在 ""演示光标"" 区调整。

```

In the `HelpBodyEn` const, insert this block immediately before the `System tray` block:

```
Presentation cursor
• Alt+F6 by default (rebindable in Shortcuts). Press once to enable, again to disable.
• Shows an enlarged high-contrast cursor at the pointer, with a small centre hole so the precise target stays visible.
• Left / middle / right clicks emit a coloured ripple so viewers see exactly where you clicked.
• Size / color / opacity / ripple tune in the Presentation cursor section.

```

- [ ] **Step 4: Build**

Run: `dotnet build src/MAXsCursor/MAXsCursor.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```
git add src/MAXsCursor/Settings/Strings.cs
git commit -m "feat: add presentation cursor strings"
```

---

## Task 8: Settings window UI

Add a Presentation cursor section (big cursor sliders + colour, ripple enable/colours/size/duration) and a presentation hotkey row in the Shortcuts section, with code-behind handlers, load, labels, capture, and reset.

**Files:**
- Modify: `src/MAXsCursor/Settings/SettingsWindow.xaml`
- Modify: `src/MAXsCursor/Settings/SettingsWindow.xaml.cs`

- [ ] **Step 1: Add the Presentation section XAML**

In `SettingsWindow.xaml`, find the closing of the HUD section. It is the `</StackPanel>` on the line immediately before:

```xml
                <TextBlock x:Name="SectionShortcutsText"
                           FontWeight="Bold" Margin="0,0,0,8" Opacity="0.85"/>
```

Insert this block between that `</StackPanel>` and the `SectionShortcutsText` TextBlock:

```xml
                <TextBlock x:Name="SectionPresentationText"
                           FontWeight="Bold" Margin="0,0,0,8" Opacity="0.85"/>

                <StackPanel Margin="0,0,0,16">
                    <StackPanel Margin="0,0,0,10">
                        <DockPanel>
                            <TextBlock x:Name="PresSizeText" FontWeight="SemiBold"/>
                            <TextBlock x:Name="PresSizeLabel" HorizontalAlignment="Right"
                                       Opacity="0.7" DockPanel.Dock="Right"/>
                        </DockPanel>
                        <Slider x:Name="PresSizeSlider" Minimum="24" Maximum="120" Value="64"
                                SmallChange="1" LargeChange="4" Margin="0,4,0,0"/>
                    </StackPanel>

                    <StackPanel Margin="0,0,0,10">
                        <DockPanel>
                            <TextBlock x:Name="PresHoleText" FontWeight="SemiBold"/>
                            <TextBlock x:Name="PresHoleLabel" HorizontalAlignment="Right"
                                       Opacity="0.7" DockPanel.Dock="Right"/>
                        </DockPanel>
                        <Slider x:Name="PresHoleSlider" Minimum="0" Maximum="40" Value="16"
                                SmallChange="1" LargeChange="4" Margin="0,4,0,0"/>
                    </StackPanel>

                    <StackPanel Margin="0,0,0,10">
                        <DockPanel>
                            <TextBlock x:Name="PresBorderText" FontWeight="SemiBold"/>
                            <TextBlock x:Name="PresBorderLabel" HorizontalAlignment="Right"
                                       Opacity="0.7" DockPanel.Dock="Right"/>
                        </DockPanel>
                        <Slider x:Name="PresBorderSlider" Minimum="0" Maximum="10" Value="3"
                                SmallChange="0.5" LargeChange="1" Margin="0,4,0,0"/>
                    </StackPanel>

                    <StackPanel Margin="0,0,0,10">
                        <DockPanel>
                            <TextBlock x:Name="PresOpacityText" FontWeight="SemiBold"/>
                            <TextBlock x:Name="PresOpacityLabel" HorizontalAlignment="Right"
                                       Opacity="0.7" DockPanel.Dock="Right"/>
                        </DockPanel>
                        <Slider x:Name="PresOpacitySlider" Minimum="0.2" Maximum="1" Value="0.85"
                                SmallChange="0.05" LargeChange="0.1" Margin="0,4,0,0"/>
                    </StackPanel>

                    <StackPanel Margin="0,0,0,12">
                        <DockPanel>
                            <TextBlock x:Name="PresColorText" FontWeight="SemiBold"/>
                            <Border x:Name="PresColorSwatch" Width="90" Height="20"
                                    BorderThickness="1" CornerRadius="3"
                                    HorizontalAlignment="Right" DockPanel.Dock="Right"
                                    Background="#FFFFC400">
                                <Border.BorderBrush>
                                    <SolidColorBrush Color="{DynamicResource TextFillColorPrimary}" Opacity="0.4"/>
                                </Border.BorderBrush>
                            </Border>
                        </DockPanel>
                        <Slider x:Name="PresHueSlider" Minimum="0" Maximum="360" Value="48"
                                SmallChange="1" LargeChange="15" Margin="0,6,0,0"/>
                        <Slider x:Name="PresSatSlider" Minimum="0" Maximum="1" Value="1"
                                SmallChange="0.05" LargeChange="0.2" Margin="0,4,0,0"/>
                        <Slider x:Name="PresLightSlider" Minimum="0.1" Maximum="0.9" Value="0.5"
                                SmallChange="0.05" LargeChange="0.1" Margin="0,4,0,0"/>
                    </StackPanel>

                    <CheckBox x:Name="RippleEnabledCheck" Margin="0,0,0,10"/>

                    <DockPanel Margin="0,0,0,8">
                        <TextBlock x:Name="RippleLeftColorText" FontWeight="SemiBold" VerticalAlignment="Center"/>
                        <Border x:Name="RippleLeftSwatch" Width="60" Height="18" BorderThickness="1"
                                CornerRadius="3" Margin="8,0,8,0" DockPanel.Dock="Right" Background="#FFFFE000">
                            <Border.BorderBrush>
                                <SolidColorBrush Color="{DynamicResource TextFillColorPrimary}" Opacity="0.4"/>
                            </Border.BorderBrush>
                        </Border>
                        <Slider x:Name="RippleLeftHueSlider" Minimum="0" Maximum="360" Value="52"
                                SmallChange="1" LargeChange="15"/>
                    </DockPanel>

                    <DockPanel Margin="0,0,0,8">
                        <TextBlock x:Name="RippleMiddleColorText" FontWeight="SemiBold" VerticalAlignment="Center"/>
                        <Border x:Name="RippleMiddleSwatch" Width="60" Height="18" BorderThickness="1"
                                CornerRadius="3" Margin="8,0,8,0" DockPanel.Dock="Right" Background="#FF00E676">
                            <Border.BorderBrush>
                                <SolidColorBrush Color="{DynamicResource TextFillColorPrimary}" Opacity="0.4"/>
                            </Border.BorderBrush>
                        </Border>
                        <Slider x:Name="RippleMiddleHueSlider" Minimum="0" Maximum="360" Value="151"
                                SmallChange="1" LargeChange="15"/>
                    </DockPanel>

                    <DockPanel Margin="0,0,0,12">
                        <TextBlock x:Name="RippleRightColorText" FontWeight="SemiBold" VerticalAlignment="Center"/>
                        <Border x:Name="RippleRightSwatch" Width="60" Height="18" BorderThickness="1"
                                CornerRadius="3" Margin="8,0,8,0" DockPanel.Dock="Right" Background="#FF2979FF">
                            <Border.BorderBrush>
                                <SolidColorBrush Color="{DynamicResource TextFillColorPrimary}" Opacity="0.4"/>
                            </Border.BorderBrush>
                        </Border>
                        <Slider x:Name="RippleRightHueSlider" Minimum="0" Maximum="360" Value="217"
                                SmallChange="1" LargeChange="15"/>
                    </DockPanel>

                    <StackPanel Margin="0,0,0,10">
                        <DockPanel>
                            <TextBlock x:Name="RippleSizeText" FontWeight="SemiBold"/>
                            <TextBlock x:Name="RippleSizeLabel" HorizontalAlignment="Right"
                                       Opacity="0.7" DockPanel.Dock="Right"/>
                        </DockPanel>
                        <Slider x:Name="RippleSizeSlider" Minimum="16" Maximum="160" Value="48"
                                SmallChange="2" LargeChange="8" Margin="0,4,0,0"/>
                    </StackPanel>

                    <StackPanel Margin="0,0,0,4">
                        <DockPanel>
                            <TextBlock x:Name="RippleDurationText" FontWeight="SemiBold"/>
                            <TextBlock x:Name="RippleDurationLabel" HorizontalAlignment="Right"
                                       Opacity="0.7" DockPanel.Dock="Right"/>
                        </DockPanel>
                        <Slider x:Name="RippleDurationSlider" Minimum="150" Maximum="900" Value="420"
                                SmallChange="10" LargeChange="50" Margin="0,4,0,0"/>
                    </StackPanel>
                </StackPanel>
```

- [ ] **Step 2: Add the presentation hotkey row XAML**

In the Shortcuts `StackPanel`, after the Zoom hotkey `</DockPanel>` (the one containing `ZoomHotkeyButton`) and before the closing `</StackPanel>` of the shortcuts block, insert:

```xml
                    <DockPanel Margin="0,10,0,0">
                        <StackPanel DockPanel.Dock="Left">
                            <TextBlock x:Name="ShortcutPresentationLabelText" FontWeight="SemiBold"/>
                            <TextBlock x:Name="ShortcutPresentationHintText" Opacity="0.7" FontSize="12"/>
                        </StackPanel>
                        <Button x:Name="PresentationHotkeyButton" MinWidth="120" Height="32" Padding="12,0"
                                HorizontalAlignment="Right"
                                Click="BeginCapturePresentation_Click"/>
                    </DockPanel>
```

- [ ] **Step 3: Wire event handlers in the constructor**

In `SettingsWindow.xaml.cs`, in the constructor, after the line `MouseButtonsCheck.Unchecked += (_, _) => OnMouseButtonsChanged();` add:

```csharp
        PresSizeSlider.ValueChanged += (_, _) => OnPresAppearanceChanged();
        PresHoleSlider.ValueChanged += (_, _) => OnPresAppearanceChanged();
        PresBorderSlider.ValueChanged += (_, _) => OnPresAppearanceChanged();
        PresOpacitySlider.ValueChanged += (_, _) => OnPresAppearanceChanged();
        PresHueSlider.ValueChanged += (_, _) => OnPresColorChanged();
        PresSatSlider.ValueChanged += (_, _) => OnPresColorChanged();
        PresLightSlider.ValueChanged += (_, _) => OnPresColorChanged();
        RippleEnabledCheck.Checked += (_, _) => OnRippleSettingsChanged();
        RippleEnabledCheck.Unchecked += (_, _) => OnRippleSettingsChanged();
        RippleLeftHueSlider.ValueChanged += (_, _) => OnRippleColorChanged();
        RippleMiddleHueSlider.ValueChanged += (_, _) => OnRippleColorChanged();
        RippleRightHueSlider.ValueChanged += (_, _) => OnRippleColorChanged();
        RippleSizeSlider.ValueChanged += (_, _) => OnRippleSettingsChanged();
        RippleDurationSlider.ValueChanged += (_, _) => OnRippleSettingsChanged();
```

- [ ] **Step 4: Load the new values in `LoadFromModel`**

In `LoadFromModel`, after the line `HudFontSizeSlider.Value = Math.Clamp(_model.HudFontSize, HudFontSizeSlider.Minimum, HudFontSizeSlider.Maximum);` add:

```csharp
        PresSizeSlider.Value = Math.Clamp(_model.BigCursorSize, PresSizeSlider.Minimum, PresSizeSlider.Maximum);
        PresHoleSlider.Value = Math.Clamp(_model.BigCursorHoleSize, PresHoleSlider.Minimum, PresHoleSlider.Maximum);
        PresBorderSlider.Value = Math.Clamp(_model.BigCursorBorderThickness, PresBorderSlider.Minimum, PresBorderSlider.Maximum);
        PresOpacitySlider.Value = Math.Clamp(_model.BigCursorOpacity, PresOpacitySlider.Minimum, PresOpacitySlider.Maximum);

        var presRgb = ColorParse.Parse(_model.BigCursorColor);
        var (ph, ps, pl) = RgbToHsl(presRgb.R, presRgb.G, presRgb.B);
        PresHueSlider.Value = ph;
        PresSatSlider.Value = ps;
        PresLightSlider.Value = Math.Clamp(pl, PresLightSlider.Minimum, PresLightSlider.Maximum);

        RippleEnabledCheck.IsChecked = _model.ClickRippleEnabled;
        RippleLeftHueSlider.Value = HueOf(_model.LeftClickColor);
        RippleMiddleHueSlider.Value = HueOf(_model.MiddleClickColor);
        RippleRightHueSlider.Value = HueOf(_model.RightClickColor);
        RippleSizeSlider.Value = Math.Clamp(_model.RippleMaxRadius, RippleSizeSlider.Minimum, RippleSizeSlider.Maximum);
        RippleDurationSlider.Value = Math.Clamp(_model.RippleDurationMs, RippleDurationSlider.Minimum, RippleDurationSlider.Maximum);
```

- [ ] **Step 5: Apply strings for the new controls**

In `ApplyStrings`, after the line `HudFontSizeText.Text = Strings.KeyFontSize;` add:

```csharp
        SectionPresentationText.Text = Strings.SectionPresentation;
        PresSizeText.Text = Strings.PresBigSize;
        PresHoleText.Text = Strings.PresHole;
        PresBorderText.Text = Strings.PresBorder;
        PresOpacityText.Text = Strings.PresOpacity;
        PresColorText.Text = Strings.PresColor;
        RippleEnabledCheck.Content = Strings.RippleEnabled;
        RippleLeftColorText.Text = Strings.RippleLeftColor;
        RippleMiddleColorText.Text = Strings.RippleMiddleColor;
        RippleRightColorText.Text = Strings.RippleRightColor;
        RippleSizeText.Text = Strings.RippleSize;
        RippleDurationText.Text = Strings.RippleDuration;
```

Still in `ApplyStrings`, after the line `ZoomHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.ZoomHotkeyMods, _model.ZoomHotkeyVk);` add:

```csharp
        ShortcutPresentationLabelText.Text = Strings.ShortcutPresentationLabel;
        ShortcutPresentationHintText.Text = Strings.ShortcutPresentationHint;
        PresentationHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.PresentationHotkeyMods, _model.PresentationHotkeyVk);
```

- [ ] **Step 6: Add the change handlers and helpers**

In `SettingsWindow.xaml.cs`, immediately before the `private void UpdateLabels()` method, add:

```csharp
    private void OnPresAppearanceChanged()
    {
        if (_suppressChange) return;
        _model.BigCursorSize = PresSizeSlider.Value;
        _model.BigCursorHoleSize = PresHoleSlider.Value;
        _model.BigCursorBorderThickness = PresBorderSlider.Value;
        _model.BigCursorOpacity = PresOpacitySlider.Value;
        UpdateLabels();
        UpdatePresSwatch();
        _onChanged(_model);
    }

    private void OnPresColorChanged()
    {
        if (_suppressChange) return;
        var (r, g, b) = HslToRgb(PresHueSlider.Value, PresSatSlider.Value, PresLightSlider.Value);
        _model.BigCursorColor = $"#{r:X2}{g:X2}{b:X2}";
        UpdatePresSwatch();
        _onChanged(_model);
    }

    private void OnRippleSettingsChanged()
    {
        if (_suppressChange) return;
        _model.ClickRippleEnabled = RippleEnabledCheck.IsChecked == true;
        _model.RippleMaxRadius = RippleSizeSlider.Value;
        _model.RippleDurationMs = (int)Math.Round(RippleDurationSlider.Value);
        UpdateLabels();
        _onChanged(_model);
    }

    private void OnRippleColorChanged()
    {
        if (_suppressChange) return;
        _model.LeftClickColor = HueToHex(RippleLeftHueSlider.Value);
        _model.MiddleClickColor = HueToHex(RippleMiddleHueSlider.Value);
        _model.RightClickColor = HueToHex(RippleRightHueSlider.Value);
        UpdateRippleSwatches();
        _onChanged(_model);
    }

    // Ripple colours are vivid: fixed saturation 1.0 and lightness 0.5, hue chosen by slider.
    private static string HueToHex(double hue)
    {
        var (r, g, b) = HslToRgb(hue, 1.0, 0.5);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static double HueOf(string hex)
    {
        var rgb = ColorParse.Parse(hex);
        var (h, _, _) = RgbToHsl(rgb.R, rgb.G, rgb.B);
        return h;
    }

    private void UpdatePresSwatch()
    {
        var rgb = ColorParse.Parse(_model.BigCursorColor);
        var alpha = (byte)Math.Round(255 * _model.BigCursorOpacity);
        PresColorSwatch.Background = new MediaBrush(MediaColor.FromArgb(alpha, rgb.R, rgb.G, rgb.B));
    }

    private void UpdateRippleSwatches()
    {
        var l = ColorParse.Parse(_model.LeftClickColor);
        var m = ColorParse.Parse(_model.MiddleClickColor);
        var r = ColorParse.Parse(_model.RightClickColor);
        RippleLeftSwatch.Background = new MediaBrush(MediaColor.FromRgb(l.R, l.G, l.B));
        RippleMiddleSwatch.Background = new MediaBrush(MediaColor.FromRgb(m.R, m.G, m.B));
        RippleRightSwatch.Background = new MediaBrush(MediaColor.FromRgb(r.R, r.G, r.B));
    }
```

- [ ] **Step 7: Update labels and swatches**

In `UpdateLabels`, after the line `HudFontSizeLabel.Text = $"{HudFontSizeSlider.Value:F0} px";` add:

```csharp
        PresSizeLabel.Text = $"{PresSizeSlider.Value:F0} px";
        PresHoleLabel.Text = $"{PresHoleSlider.Value:F0} px";
        PresBorderLabel.Text = $"{PresBorderSlider.Value:F1} px";
        PresOpacityLabel.Text = $"{PresOpacitySlider.Value * 100:F0}%";
        RippleSizeLabel.Text = $"{RippleSizeSlider.Value:F0} px";
        RippleDurationLabel.Text = $"{RippleDurationSlider.Value:F0} ms";
```

In `LoadFromModel`, find the existing calls near the end:

```csharp
        UpdateLabels();
        UpdateSwatch();
        UpdatePositionStatus();
```

and replace with:

```csharp
        UpdateLabels();
        UpdateSwatch();
        UpdatePresSwatch();
        UpdateRippleSwatches();
        UpdatePositionStatus();
```

- [ ] **Step 8: Extend the hotkey capture enum and handlers**

Replace this line:

```csharp
    private enum HotkeyTarget { None, Toggle, Zoom }
```

with:

```csharp
    private enum HotkeyTarget { None, Toggle, Zoom, Presentation }
```

After the method `private void BeginCaptureZoom_Click(object sender, RoutedEventArgs e) => BeginCapture(HotkeyTarget.Zoom);` add:

```csharp
    private void BeginCapturePresentation_Click(object sender, RoutedEventArgs e) => BeginCapture(HotkeyTarget.Presentation);
```

In `BeginCapture`, replace this line:

```csharp
        (target == HotkeyTarget.Toggle ? ToggleHotkeyButton : ZoomHotkeyButton).Content = Strings.ShortcutCapturePrompt;
```

with:

```csharp
        CaptureButton(target).Content = Strings.ShortcutCapturePrompt;
```

In `EndCapture`, replace the commit block:

```csharp
        if (commit && target != HotkeyTarget.None)
        {
            if (target == HotkeyTarget.Toggle)
            {
                _model.ToggleHotkeyMods = mods;
                _model.ToggleHotkeyVk = vk;
            }
            else
            {
                _model.ZoomHotkeyMods = mods;
                _model.ZoomHotkeyVk = vk;
            }
            _onChanged(_model);
        }

        // Restore labels (either to the committed new value or back to what was there).
        ToggleHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.ToggleHotkeyMods, _model.ToggleHotkeyVk);
        ZoomHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.ZoomHotkeyMods, _model.ZoomHotkeyVk);
```

with:

```csharp
        if (commit && target != HotkeyTarget.None)
        {
            switch (target)
            {
                case HotkeyTarget.Toggle:
                    _model.ToggleHotkeyMods = mods;
                    _model.ToggleHotkeyVk = vk;
                    break;
                case HotkeyTarget.Zoom:
                    _model.ZoomHotkeyMods = mods;
                    _model.ZoomHotkeyVk = vk;
                    break;
                case HotkeyTarget.Presentation:
                    _model.PresentationHotkeyMods = mods;
                    _model.PresentationHotkeyVk = vk;
                    break;
            }
            _onChanged(_model);
        }

        // Restore labels (either to the committed new value or back to what was there).
        ToggleHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.ToggleHotkeyMods, _model.ToggleHotkeyVk);
        ZoomHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.ZoomHotkeyMods, _model.ZoomHotkeyVk);
        PresentationHotkeyButton.Content = KeyTranslator.FormatHotkey(_model.PresentationHotkeyMods, _model.PresentationHotkeyVk);
```

In `OnCaptureKey`, replace this line:

```csharp
            var btn = _captureTarget == HotkeyTarget.Toggle ? ToggleHotkeyButton : ZoomHotkeyButton;
```

with:

```csharp
            var btn = CaptureButton(_captureTarget);
```

Then add this helper immediately after the `OnCaptureKey` method:

```csharp
    private System.Windows.Controls.Button CaptureButton(HotkeyTarget target) => target switch
    {
        HotkeyTarget.Zoom => ZoomHotkeyButton,
        HotkeyTarget.Presentation => PresentationHotkeyButton,
        _ => ToggleHotkeyButton
    };
```

- [ ] **Step 9: Extend Reset**

In `Reset_Click`, after the line `_model.ZoomHotkeyVk = defaults.ZoomHotkeyVk;` add:

```csharp
        _model.PresentationHotkeyMods = defaults.PresentationHotkeyMods;
        _model.PresentationHotkeyVk = defaults.PresentationHotkeyVk;
        _model.BigCursorSize = defaults.BigCursorSize;
        _model.BigCursorHoleSize = defaults.BigCursorHoleSize;
        _model.BigCursorBorderThickness = defaults.BigCursorBorderThickness;
        _model.BigCursorColor = defaults.BigCursorColor;
        _model.BigCursorBorderColor = defaults.BigCursorBorderColor;
        _model.BigCursorOpacity = defaults.BigCursorOpacity;
        _model.ClickRippleEnabled = defaults.ClickRippleEnabled;
        _model.LeftClickColor = defaults.LeftClickColor;
        _model.MiddleClickColor = defaults.MiddleClickColor;
        _model.RightClickColor = defaults.RightClickColor;
        _model.RippleMaxRadius = defaults.RippleMaxRadius;
        _model.RippleDurationMs = defaults.RippleDurationMs;
```

- [ ] **Step 10: Build**

Run: `dotnet build src/MAXsCursor/MAXsCursor.csproj`
Expected: Build succeeded. Fix any XAML name typos the compiler reports (the generated fields come from `x:Name`).

- [ ] **Step 11: Commit**

```
git add src/MAXsCursor/Settings/SettingsWindow.xaml src/MAXsCursor/Settings/SettingsWindow.xaml.cs
git commit -m "feat: add presentation cursor settings UI"
```

---

## Task 9: App wiring (toggle, hotkey, frame tick, ripple spawn, settings apply)

Tie everything together in `App.xaml.cs`: create the big cursor and ripple controller, register the presentation hotkey, toggle the mode, spawn ripples from drained clicks, and live-apply settings. Big cursor border colour stays at the model default white (no UI control), giving the classic high-contrast look.

**Files:**
- Modify: `src/MAXsCursor/App.xaml.cs`

- [ ] **Step 1: Add fields**

After the field `private ZoomWindow? _zoomWindow;` add:

```csharp
    private ClickRippleController? _ripple;
```

After the field `private bool _enabled = true;` add:

```csharp
    private bool _presentationOn;
```

After the line `private int? _zoomHotkeyId;` add:

```csharp
    private int? _presentationHotkeyId;
```

- [ ] **Step 2: Initialize the big cursor and ripple controller in `OnStartup`**

In `OnStartup`, immediately after the `_hook.InitializeCursor(...)` call (the block that ends with `});`) and before the `// Render clock drives HUD fade only.` comment, add:

```csharp
        _hook.InitializeBigCursor(sizeDip: 220, dpiScale: dpiScale, configure: bc =>
        {
            var fill = ColorParse.Parse(initial.BigCursorColor);
            var border = ColorParse.Parse(initial.BigCursorBorderColor);
            bc.ApplyAppearance(
                fill.R, fill.G, fill.B, border.R, border.G, border.B,
                initial.BigCursorSize, initial.BigCursorHoleSize,
                initial.BigCursorBorderThickness, initial.BigCursorOpacity, dpiScale);
        });

        _ripple = new ClickRippleController(dpiScale);
        ApplyRippleSettings();
```

- [ ] **Step 3: Tick ripples each frame**

In `OnFrameTick`, after the line `_hud?.TickHud();` add:

```csharp
        _ripple?.Tick();
```

- [ ] **Step 4: Spawn ripples from drained clicks**

In `DrainMouseButtonsNow`, replace this loop body:

```csharp
        while (_bus.TryDequeueMouseButton(out var btn))
        {
            // Always drain, never back-pressure. Just skip UI if user disabled the setting.
            if (!_settings.ShowMouseButtons) continue;
            var text = KeyTranslator.MouseToDisplayText(btn.Button, btn.Modifiers);
            if (text is not null)
            {
                _hud.PushKeyChip(text);
                pushed = true;
            }
        }
```

with:

```csharp
        while (_bus.TryDequeueMouseButton(out var btn))
        {
            // Presentation ripple is independent of the HUD's ShowMouseButtons toggle.
            if (_presentationOn && _enabled)
            {
                _ripple?.Spawn(btn.Button, btn.X, btn.Y);
            }

            // Always drain, never back-pressure. Just skip the HUD if the user disabled it.
            if (!_settings.ShowMouseButtons) continue;
            var text = KeyTranslator.MouseToDisplayText(btn.Button, btn.Modifiers);
            if (text is not null)
            {
                _hud.PushKeyChip(text);
                pushed = true;
            }
        }
```

- [ ] **Step 5: Add settings-apply helpers**

After the `ApplyCursorSettings` method, add:

```csharp
    private void ApplyBigCursorSettings()
    {
        if (_hook is null) return;
        var fill = ColorParse.Parse(_settings.BigCursorColor);
        var border = ColorParse.Parse(_settings.BigCursorBorderColor);
        _hook.ApplyBigCursor(
            fill.R, fill.G, fill.B, border.R, border.G, border.B,
            _settings.BigCursorSize, _settings.BigCursorHoleSize,
            _settings.BigCursorBorderThickness, _settings.BigCursorOpacity, DetectDpiScale());
    }

    private void ApplyRippleSettings()
    {
        if (_ripple is null) return;
        var l = ColorParse.Parse(_settings.LeftClickColor);
        var m = ColorParse.Parse(_settings.MiddleClickColor);
        var r = ColorParse.Parse(_settings.RightClickColor);
        _ripple.ApplySettings(
            _settings.ClickRippleEnabled, _settings.RippleMaxRadius, _settings.RippleDurationMs,
            (l.R, l.G, l.B), (m.R, m.G, m.B), (r.R, r.G, r.B));
    }
```

Note: `ColorParse.Parse` returns a WPF `System.Windows.Media.Color`, whose `R`/`G`/`B` are `byte`, matching the `byte` parameters and the tuple element types.

- [ ] **Step 6: Apply the new settings in `OnSettingsChanged`**

In `OnSettingsChanged`, after this existing block:

```csharp
        var zoomChanged = _settings.ZoomHotkeyMods != updated.ZoomHotkeyMods
                       || _settings.ZoomHotkeyVk != updated.ZoomHotkeyVk;
```

add:

```csharp
        var presentationChanged = _settings.PresentationHotkeyMods != updated.PresentationHotkeyMods
                               || _settings.PresentationHotkeyVk != updated.PresentationHotkeyVk;
```

Then change this condition:

```csharp
        if (toggleChanged || zoomChanged)
        {
            RegisterConfiguredHotkeys();
        }
```

to:

```csharp
        if (toggleChanged || zoomChanged || presentationChanged)
        {
            RegisterConfiguredHotkeys();
        }
```

And after the `ApplyCursorSettings();` call in the same method add:

```csharp
        ApplyBigCursorSettings();
        ApplyRippleSettings();
```

- [ ] **Step 7: Register the presentation hotkey**

In `RegisterConfiguredHotkeys`, after this block:

```csharp
        if (_zoomHotkeyId.HasValue) { _hotkey.Unregister(_zoomHotkeyId.Value); _zoomHotkeyId = null; }
```

add:

```csharp
        if (_presentationHotkeyId.HasValue) { _hotkey.Unregister(_presentationHotkeyId.Value); _presentationHotkeyId = null; }
```

And after the zoom hotkey registration block (ending with the zoom `Log(...)` line) add:

```csharp
        _presentationHotkeyId = _hotkey.Register(_settings.PresentationHotkeyMods, _settings.PresentationHotkeyVk,
            () => { if (!_hotkeyCaptureActive) TogglePresentation(); });
        if (_presentationHotkeyId is null) Log($"WARN: presentation hotkey registration failed: {_settings.PresentationHotkeyMods:X}/{_settings.PresentationHotkeyVk:X}");
```

- [ ] **Step 8: Add `TogglePresentation` and update `ToggleEnabled`**

After the `ToggleEnabled` method add:

```csharp
    private void TogglePresentation()
    {
        Dispatcher.Invoke(() =>
        {
            _presentationOn = !_presentationOn;
            _hook?.SetBigCursorVisible(_enabled && _presentationOn);
            if (!_presentationOn) _ripple?.Clear();
        });
    }
```

In `ToggleEnabled`, after the line `_tray?.SetEnabled(_enabled);` add:

```csharp
            _hook?.SetBigCursorVisible(_enabled && _presentationOn);
            if (!_enabled) _ripple?.Clear();
```

- [ ] **Step 9: Dispose the ripple controller on shutdown**

In `CleanupHooks`, after the line `_clock = null;` add:

```csharp
        _ripple?.Dispose();
        _ripple = null;
```

- [ ] **Step 10: Build**

Run: `dotnet build src/MAXsCursor/MAXsCursor.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 11: Commit**

```
git add src/MAXsCursor/App.xaml.cs
git commit -m "feat: wire presentation cursor toggle, hotkey, and ripples"
```

---

## Task 10: Manual acceptance pass

**Files:** none (verification only).

- [ ] **Step 1: Run the app**

Run: `dotnet run --project src/MAXsCursor/MAXsCursor.csproj`
Expected: tray icon appears, cursor ring shows as before.

- [ ] **Step 2: Toggle the mode**

Press Alt+F6. Expected: a large high-contrast disk with a small centre hole appears at the pointer and follows it smoothly. Press Alt+F6 again: it disappears.

- [ ] **Step 3: Centre hole and contrast**

With the mode on, hover over both a bright and a dark window. Expected: the border keeps the cursor readable on both, and the centre hole shows the target underneath.

- [ ] **Step 4: Click ripples**

With the mode on, left-click, middle-click, and right-click. Expected: each emits an expanding, fading ring in its own colour (yellow / green / blue by default). Rapid-click many times: no leaked or stuck rings, at most four concurrent.

- [ ] **Step 5: Multi-monitor and DPI**

Move the pointer to a second monitor (ideally a different DPI). Expected: the big cursor follows and stays correctly sized; ripples appear at the click point.

- [ ] **Step 6: Master toggle interaction**

With presentation mode on, press Alt+F5 (master off). Expected: big cursor and ripples disappear along with the ring and HUD. Press Alt+F5 again. Expected: big cursor returns (mode was still on).

- [ ] **Step 7: Live settings**

Open Settings, in the Presentation cursor section change size, hole, opacity, and colour, and the three ripple colours, size, and duration. Expected: big cursor updates live; next clicks use the new ripple colours/size/duration. Toggle Click ripple off: clicks no longer ripple.

- [ ] **Step 8: Rebind hotkey**

In Shortcuts, rebind Presentation cursor to another combo. Expected: the new combo toggles the mode; Alt+F6 no longer does.

- [ ] **Step 9: Persistence and off-by-default**

Close and reopen the app. Expected: appearance settings persisted; the mode starts off (no big cursor until Alt+F6).

- [ ] **Step 10: Performance**

Open Task Manager Performance tab. With the mode on and the pointer still, GPU usage is negligible. While clicking rapidly, it stays within the overlay budget with no visible frame drops in a GPU-heavy app.

- [ ] **Step 11: Final commit (docs/checklist only if anything changed)**

If steps surfaced no code changes, no commit is needed. If a fix was required, commit it with a `fix:` message describing the correction.
```
