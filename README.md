<p align="center">
  <img src="assets/cursor.png" alt="MAXsCursor icon" width="96"/>
</p>

# MAXsCursor

**English | [中文](README.zh-CN.md)**

Low-latency cursor highlight and keyboard shortcut overlay for Windows. Built for recording teaching videos at high refresh rates (144 Hz / 240 Hz / 260 Hz) in GPU-heavy applications like UE5, Maya, Substance Designer, and Blender.

.NET 10 + WPF + pure Win32 layered window. No third-party UI frameworks, no telemetry, no network calls.

## Features

- **Cursor highlight ring** that tracks the system pointer with near-zero latency. Ring color, radius, thickness, and opacity are all live-tunable.
- **Shortcut HUD** at the bottom-center of whichever monitor the cursor is on. Shows the last few keypresses as single, merged chips: `Ctrl+Shift+S`, not three separate keys. Lone modifiers (`Alt`, `Ctrl`, ...) show as their own chip if pressed and released without combining.
- **Mouse button chips**: `Left click`, `Right click`, `Middle click`, `Wheel ↑ / ↓`, `Ctrl+Left click`, etc. Independently toggleable.
- **Multi-monitor aware**: HUD follows the cursor's display. Mixed DPI is handled in physical pixels to avoid drift.
- **Global toggle**: `Alt+F5` turns the whole overlay on and off, even when another app has focus.
- **System tray icon** with enable / disable / settings / quit, plus a colored ring when enabled and a grayscale one when disabled.
- **Settings window** with live preview, HSL color picker, custom HUD position picker, language switch (中文 / English), and auto system dark-mode via WPF Fluent theme.
- **Settings persist** to `%APPDATA%\MAXsCursor\settings.json`.

## Why another cursor overlay

Typical WPF transparent-overlay tools drop frames under Maya 2026 and similar DirectX apps because they run through WPF's layered-window composition path. MAXsCursor runs the cursor overlay as a **native Win32 `WS_EX_LAYERED` window owned by the hook thread**, with the bitmap uploaded once via `UpdateLayeredWindow` and subsequent movement driven by `SetWindowPos` directly from the `WH_MOUSE_LL` callback. No UI-thread marshaling, no DWM-frame latency. This is the approach used by tools like PointerFocus, rebuilt cleanly in C#.

## Download

See the [Releases](../../releases) page for a self-contained single-file `MAXsCursor.exe` (~73 MB, includes the .NET 10 runtime). No installer. Double-click to run.

## Build from source

Requires .NET 10 SDK. From the repo root:

```powershell
dotnet build src\MAXsCursor\MAXsCursor.csproj -c Debug
# or for a release single-file exe:
dotnet publish src\MAXsCursor\MAXsCursor.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=embedded
```

Publish output lands in `src\MAXsCursor\bin\Release\net10.0-windows\win-x64\publish\MAXsCursor.exe`.

To refresh the application icon after editing `src\MAXsCursor\Assets\cursor.png`:

```powershell
.\make-icon.ps1
```

## Project layout

```
src/MAXsCursor/
  Program.cs               entry point + single-instance mutex
  App.xaml(.cs)            orchestration: wires hook, HUD, tray, hotkey, settings
  Core/
    HookManager.cs         dedicated thread owning WH_MOUSE_LL + WH_KEYBOARD_LL + cursor window
    EventBus.cs            lock-free queues between hook thread and UI thread
    HotkeyManager.cs       Alt+F5 via RegisterHotKey on a message-only window
    KeyTranslator.cs       VK + modifiers -> display chip text
  Overlay/
    NativeCursorWindow.cs  pure Win32 layered-window overlay, GDI+ into DIB
    HudWindow.xaml(.cs)    WPF HUD window, per-monitor follow
    KeyboardHudLayer.cs    chip row with 2 s visible + 0.6 s linear fade
    RenderClock.cs         CompositionTarget.Rendering wrapper
  Settings/
    SettingsModel.cs       POCO; System.Text.Json round-trip
    SettingsStore.cs       load/save settings.json
    SettingsWindow.xaml    configuration UI with live preview
    HudPositionPicker.xaml draggable floating panel for pinning HUD location
    Strings.cs             zh / en string table
  Tray/
    TrayIcon.cs            WinForms NotifyIcon wrapper
  Interop/
    Win32.cs               all P/Invoke declarations
    WindowStyles.cs        WS_EX_* / SWP_* / WM_* constants
```

Design rationale and performance constraints are documented in [SPEC.md](SPEC.md). Working rules for Claude Code contributions (and human contributors) are in [CLAUDE.md](CLAUDE.md).

## License

MIT. See [LICENSE](LICENSE).
