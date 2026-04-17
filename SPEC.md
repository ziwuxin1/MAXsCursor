# MAXsCursor — Product Specification

## 1. Product Overview

**MAXsCursor** is a low-latency cursor and keyboard visualization overlay for Windows, designed for recording technical teaching content (UE5, Substance Designer, Blender, etc.) at high refresh rates (144Hz / 240Hz / 260Hz).

**Target user**: Junliang (solo user). Personal tool for recording MAXs Education courses.

**Non-goals**: No installer, no code signing, no auto-update, no multi-user, no cloud sync, no licensing.

---

## 2. Hard Requirements

### Performance
- Must render smoothly at **260Hz** (frame budget: 3.8ms)
- Overlay frame cost must stay **under 1.5ms** on RTX 5090 / 9950X
- Zero perceptible input latency on cursor highlight (< 5ms mouse-to-screen)
- Must NOT drop frames in UE5 viewport when overlay is active

### Correctness
- **Click-through**: cursor clicks must pass through the overlay to the app below
- **Never steals focus**: activating the overlay must not change the active window
- **Always on top**: renders above fullscreen exclusive UE5 editor and Substance Designer
- **Per-monitor DPI aware**: correct scale on mixed 4K + 1440p setups
- **Multi-monitor**: tracks cursor across all monitors seamlessly

### Reliability
- Global hotkey toggle works even when any app has focus
- If overlay crashes, must not crash the host apps
- Hooks must be unregistered cleanly on exit (no zombie hooks)

---

## 3. Feature Set (v1 MVP)

### 3.1 Cursor Highlight Ring
- Soft-edged circle follows cursor in real time
- **Single mode**: subtle static ring around cursor, always visible when enabled
- Configurable: ring color, ring radius (px), stroke width, opacity, optional soft outer glow
- No click pulse, no animation on click events. Just a clean ring that tracks the cursor.

### 3.2 Keyboard Display
- Shows last 5 key presses in a bottom-right HUD panel
- Shows modifier combos as a single unit: `Ctrl+Shift+S`, not three separate keys
- Each key fades out after 2 seconds (configurable)
- Filters out noise: ignores auto-repeat of held keys after first press
- Handles special keys: arrows, function keys, media keys (display readable names)

### 3.3 Global Toggle
- Default hotkey: **Alt+F5** (toggle all effects on/off)
- Tray icon reflects state (colored = on, grayed = off)
- Starts enabled by default on app launch

### 3.4 System Tray
- Right-click menu:
  - Enable / Disable (same as hotkey)
  - Settings... (opens settings window)
  - Quit
- Double-click tray icon = toggle

### 3.5 Settings Window
- Minimal WPF window with live preview
- Sections: Cursor Ring, Keyboard HUD, Hotkeys
- Settings persist to `%APPDATA%\MAXsCursor\settings.json`
- Reset to defaults button

---

## 4. Technical Architecture

### Stack
- **Language**: C# 12 (.NET 8)
- **UI framework**: WPF (default DirectComposition rendering)
- **Build**: `dotnet build` / `dotnet publish -r win-x64 --self-contained false`
- **Target runtime**: Windows 10 20H2+ and Windows 11

### Key Windows APIs

| Need | API |
|------|-----|
| Global mouse hook | `SetWindowsHookEx(WH_MOUSE_LL, ...)` |
| Global keyboard hook | `SetWindowsHookEx(WH_KEYBOARD_LL, ...)` |
| Global hotkey | `RegisterHotKey` |
| Transparent overlay window | `WS_EX_LAYERED \| WS_EX_TRANSPARENT \| WS_EX_NOACTIVATE \| WS_EX_TOPMOST` |
| Click-through | `WS_EX_TRANSPARENT` extended style |
| Per-monitor DPI | `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)` |
| Multi-monitor bounds | `EnumDisplayMonitors` / `SystemParameters.VirtualScreen*` |
| High-precision timing | `QueryPerformanceCounter` via `Stopwatch` |

### Threading Model
- **Hook thread**: dedicated thread with message pump, owns `WH_MOUSE_LL` and `WH_KEYBOARD_LL`. Hook callbacks MUST return quickly (< 1ms) — just enqueue events and return.
- **UI thread**: WPF dispatcher. Drains the event queue via `CompositionTarget.Rendering` event.
- **No `Thread.Sleep` in hook callbacks.** No allocations in hot path.

### Rendering
- Single fullscreen overlay window per monitor (layered + transparent + topmost)
- Draw with WPF `DrawingVisual` or cache-friendly `CompositionTarget.Rendering` tick
- Keep the visual tree flat: no data bindings in the render loop
- Pre-build brush and pen objects, freeze them (`Freezable.Freeze()`)
- Animate via per-frame dirty rect, not WPF storyboard animations (storyboards have scheduling overhead)

### Why NOT Direct2D/SharpDX
WPF with DirectComposition is already hardware composited and fast enough. Adding Direct2D adds a dependency and complexity without meaningful performance gain for this use case. If profiling shows WPF is the bottleneck, then migrate.

---

## 5. Project Layout

```
MAXsCursor/
  MAXsCursor.sln
  src/
    MAXsCursor/
      MAXsCursor.csproj          # WPF app, net8.0-windows
      App.xaml / App.xaml.cs
      Program.cs                 # entry point + single-instance check
      Core/
        HookManager.cs           # low-level mouse/keyboard hooks
        EventBus.cs              # queues input events for UI thread
        HotkeyManager.cs         # RegisterHotKey wrapper
        MonitorInfo.cs           # multi-monitor enumeration
      Overlay/
        OverlayWindow.xaml       # fullscreen transparent click-through window
        OverlayWindow.xaml.cs
        CursorRingLayer.cs       # static ring around cursor
        KeyboardHudLayer.cs      # bottom-right keyboard display
        RenderClock.cs           # wraps CompositionTarget.Rendering
      Settings/
        SettingsStore.cs         # JSON load/save
        SettingsModel.cs         # POCO
        SettingsWindow.xaml      # configuration UI
        SettingsWindow.xaml.cs
      Tray/
        TrayIcon.cs              # NotifyIcon via WinForms interop
      Interop/
        Win32.cs                 # P/Invoke declarations
        WindowStyles.cs          # WS_EX_* constants
```

---

## 6. Build & Run

```powershell
# One-time: install .NET 8 SDK
# winget install Microsoft.DotNet.SDK.8

cd MAXsCursor
dotnet build
dotnet run --project src/MAXsCursor
```

**Hot reload during development**: use `dotnet watch run --project src/MAXsCursor`

---

## 7. Acceptance Checklist (when Claude Code claims "done")

Before calling v1 complete, manually verify:

- [ ] Launch app, see tray icon appear
- [ ] Cursor ring visible, follows cursor in UE5 editor at 260Hz without tearing
- [ ] Press Ctrl+Shift+S in any app, see `Ctrl+Shift+S` in bottom-right HUD
- [ ] Hold a key, HUD shows it once, not spammed by auto-repeat
- [ ] Press Alt+F5, all effects disappear. Press again, come back
- [ ] Double-click tray icon, same toggle behavior
- [ ] Open Settings, change ring color to red, see live change in overlay
- [ ] Close app, reopen: settings persisted
- [ ] Move cursor to second monitor, overlay follows correctly at that monitor's DPI
- [ ] Open Task Manager Performance tab: MAXsCursor GPU usage < 1% idle, < 2% while moving cursor

---

## 8. Stretch (v2, do NOT build in v1)

- Click pulse animation (expanding ring on mouse down, different color per button)
- Presets: "UE5 Teaching", "Substance Teaching", "Blender Teaching" (different color/size profiles)
- Per-monitor independent configuration
- OBS WebSocket integration (auto-enable when recording starts)
- Cursor trail/motion line
- Spotlight / zoom lens (press-and-hold to magnify area)
- Freehand annotation (like ZoomIt)

---

## 9. Constraints & Style

- **No semicolons in written English output** (author preference).
- **No em dashes** in any output (author preference).
- Code comments in English. User-facing UI strings in English for v1.
- Keep dependencies minimal. Allowed: `System.Text.Json`, `Microsoft.Extensions.*` only if strictly needed. No third-party UI frameworks, no MVVM frameworks (use plain code-behind).
- No telemetry. No network calls of any kind.
