<p align="center">
  <img src="assets/cursor.png" alt="MAXsCursor" width="160"/>
</p>

<h1 align="center">MAXs Cursor</h1>

<p align="center">
  <b>Low-latency cursor highlight and keyboard shortcut overlay for Windows.</b>
</p>

<p align="center">
  <a href="https://github.com/ziwuxin1/MAXsCursor/stargazers"><img src="https://img.shields.io/github/stars/ziwuxin1/MAXsCursor?style=flat&logo=github&color=ff6b6b" alt="stars"/></a>
  <a href="https://github.com/ziwuxin1/MAXsCursor/releases"><img src="https://img.shields.io/github/v/release/ziwuxin1/MAXsCursor?style=flat&color=ff6b6b" alt="release"/></a>
  <a href="https://github.com/ziwuxin1/MAXsCursor/releases"><img src="https://img.shields.io/github/downloads/ziwuxin1/MAXsCursor/total?style=flat&color=brightgreen" alt="downloads"/></a>
  <a href="https://github.com/ziwuxin1/MAXsCursor/issues"><img src="https://img.shields.io/github/issues/ziwuxin1/MAXsCursor?style=flat&color=red" alt="issues"/></a>
  <img src="https://img.shields.io/badge/runtime-.NET_10-blueviolet?style=flat" alt="runtime"/>
  <img src="https://img.shields.io/badge/platform-Windows_10/11-0078d4?style=flat&logo=windows" alt="platform"/>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/ziwuxin1/MAXsCursor?style=flat" alt="license"/></a>
</p>

<p align="center">
  <b>English | <a href="README.zh-CN.md">中文</a></b>
</p>

---

## Why this repo

Screencast tools that highlight the cursor and keyboard shortcuts (PointerFocus, Croncc, Carnac, KeyCastr-for-Windows, ...) either drop frames on high-refresh-rate displays or start tearing inside GPU-heavy apps like Maya 2026, UE5, Substance Designer. Most are built on WPF or Qt transparent overlays, and both composition paths lose to DirectX independent flip.

**MAXs Cursor** runs the cursor overlay as a **native Win32 `WS_EX_LAYERED` window owned directly by the low-level hook thread**. The bitmap is uploaded once via `UpdateLayeredWindow`, and every mouse-move goes straight from the `WH_MOUSE_LL` callback to `SetWindowPos` on the same thread. No UI-thread marshaling, no DWM-frame latency, no WPF render pipeline in the hot path. Tested clean at 260 Hz inside Maya viewport.

---

## Features

- **Cursor highlight ring** — tracks the system pointer with near-zero latency. Ring color / radius / thickness / opacity live-tunable.
- **Shortcut HUD** — last few keypresses at the bottom-center of whichever monitor the cursor is on. Merged chips: `Ctrl+Shift+S`, not three keys. Bare modifiers (`Alt`, `Ctrl` ...) shown as their own chip if pressed and released alone.
- **Mouse-button chips** — `Left click`, `Right click`, `Middle click`, `Wheel ↑ / ↓`, `Ctrl+Left click`, etc. Independent toggle.
- **Multi-monitor** — HUD follows the cursor's display. Mixed DPI handled in physical pixels.
- **Global toggle** — `Alt+F5` turns the whole overlay on and off from any app.
- **System tray** — colored icon when enabled, grayscale when disabled. Right-click menu with enable / settings / quit.
- **Settings window** — live preview, HSL color picker, draggable HUD-position picker, 中文 / English switcher, auto system dark-mode (WPF Fluent theme).
- **Zero dependencies** — pure .NET 10 + WPF + Win32 P/Invoke. No MVVM framework, no third-party UI libraries, no telemetry, no network calls.

---

## Download

Head to the **[Releases](../../releases)** page and grab one:

| File | Type | Size | Best for |
|---|---|---|---|
| `MAXsCursor.exe` | Portable (self-contained) | ~73 MB | Double-click to run. Put it on a USB stick. Zero install. |
| `MAXsCursor-Setup-v1.0.exe` | Installer (per-user, no admin) | ~68 MB | Standard Start-menu entry + Windows **Apps & Features** uninstall. |

Both embed the .NET 10 runtime. Nothing extra to install.

---

## Usage

1. Launch the exe (or run the installer and launch from Start menu).
2. A cursor ring appears around your pointer.
3. The tray gets a colored cursor icon. Right-click it for the menu.
4. Press `Alt+F5` any time to toggle the whole overlay on / off.
5. Right-click tray → **Settings...** to change color, size, HUD position, language, and more. All previews are live.

---

## Build from source

Requires **.NET 10 SDK**.

```powershell
# Debug build
dotnet build src\MAXsCursor\MAXsCursor.csproj -c Debug

# Release single-file exe
dotnet publish src\MAXsCursor\MAXsCursor.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=embedded
```

Publish output lands in `src\MAXsCursor\bin\Release\net10.0-windows\win-x64\publish\MAXsCursor.exe`.

**Regenerate the app icon** after editing `src\MAXsCursor\Assets\cursor.png`:

```powershell
.\make-icon.ps1
```

**Build the installer** (needs [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\MAXsCursor.iss
```

Output: `installer\MAXsCursor-Setup-v1.0.exe`.

---

## Project layout

```
src/MAXsCursor/
  Program.cs               entry point + single-instance mutex
  App.xaml(.cs)            orchestration: hook + HUD + tray + hotkey + settings
  Core/
    HookManager.cs         dedicated thread owning WH_MOUSE_LL + WH_KEYBOARD_LL + cursor window
    EventBus.cs            lock-free queues between hook thread and UI thread
    HotkeyManager.cs       Alt+F5 via RegisterHotKey on a message-only window
    KeyTranslator.cs       VK + modifiers to chip text
  Overlay/
    NativeCursorWindow.cs  pure Win32 layered-window overlay, GDI+ into DIB
    HudWindow.xaml(.cs)    WPF HUD window, per-monitor follow
    KeyboardHudLayer.cs    chip row with 2 s visible + 0.6 s fade
    RenderClock.cs         CompositionTarget.Rendering wrapper
  Settings/
    SettingsModel.cs       POCO, System.Text.Json round-trip
    SettingsStore.cs       settings.json load / save
    SettingsWindow.xaml    live-preview configuration UI
    HudPositionPicker.xaml draggable floating panel to pin HUD location
    Strings.cs             zh / en string table
  Tray/
    TrayIcon.cs            WinForms NotifyIcon wrapper
  Interop/
    Win32.cs               all P/Invoke declarations
    WindowStyles.cs        WS_EX_* / SWP_* / WM_* constants
```

Design rationale and performance budget live in [SPEC.md](SPEC.md). Working rules for contributors (human or AI) live in [CLAUDE.md](CLAUDE.md).

---

## License

[MIT](LICENSE)

<p align="center">
  <sub>Built for recording teaching videos. Made with care at 260 Hz.</sub>
</p>
