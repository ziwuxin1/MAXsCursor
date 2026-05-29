<p align="center">
  <img src="assets/cursor.png" alt="MAXsCursor" width="160"/>
</p>

<h1 align="center">MAXs Cursor</h1>

<p align="center">
  <b>Low-latency cursor highlight and keyboard shortcut overlay for Windows.</b><br/>
  <sub>Windows 平台低延迟鼠标高亮 + 快捷键显示叠加工具</sub>
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
  <a href="#english">English</a> · <a href="#中文">中文</a>
</p>

---

<a id="english"></a>

## Why this repo

Screencast tools that highlight the cursor and keyboard shortcuts (PointerFocus, Croncc, Carnac, KeyCastr-for-Windows, ...) either drop frames on high-refresh-rate displays or tear inside GPU-heavy apps like Maya 2026, UE5, Substance Designer. Most are built on WPF or Qt transparent overlays, and both composition paths lose to DirectX independent flip.

**MAXs Cursor** runs the cursor overlay as a **native Win32 `WS_EX_LAYERED` window owned directly by the low-level hook thread**. The bitmap is uploaded once via `UpdateLayeredWindow`, and every mouse-move goes straight from the `WH_MOUSE_LL` callback to `SetWindowPos` on the same thread. No UI-thread marshaling, no DWM-frame latency, no WPF render pipeline in the hot path. Tested clean at 260 Hz inside Maya viewport.

## Features

- **Cursor highlight ring** — tracks the system pointer with near-zero latency. Ring color / radius / thickness / opacity live-tunable.
- **Shortcut HUD** — last few keypresses at the bottom-center of whichever monitor the cursor is on. Merged chips: `Ctrl+Shift+S`, not three keys. Bare modifiers (`Alt`, `Ctrl` ...) shown as their own chip if pressed and released alone.
- **Mouse-button chips** — `Left click`, `Right click`, `Middle click`, `Wheel ↑ / ↓`, `Ctrl+Left click`, etc. Independent toggle.
- **Multi-monitor** — HUD follows the cursor's display. Mixed DPI handled in physical pixels.
- **Global toggle** — `Alt+F5` turns the whole overlay on and off from any app.
- **System tray** — colored icon when enabled, grayscale when disabled. Right-click menu with enable / settings / quit.
- **Settings window** — live preview, HSL color picker, draggable HUD-position picker, 中文 / English switcher, auto system dark-mode (WPF Fluent theme).
- **Zero dependencies** — pure .NET 10 + WPF + Win32 P/Invoke. No MVVM framework, no third-party UI libraries, no telemetry, no network calls.

## Download

Head to the **[Releases](../../releases)** page and grab one:

| File | Type | Size | Best for |
|---|---|---|---|
| `MAXsCursor.exe` | Portable (self-contained) | ~73 MB | Double-click to run. Put it on a USB stick. Zero install. |
| `MAXsCursor-Setup-v1.2.1.exe` | Installer (per-user, no admin) | ~68 MB | Standard Start-menu entry + Windows **Apps & Features** uninstall. |

Both embed the .NET 10 runtime. Nothing extra to install.

## Usage

1. Launch the exe (or run the installer and launch from Start menu).
2. A cursor ring appears around your pointer.
3. The tray gets a colored cursor icon. Right-click it for the menu.
4. Press `Alt+F5` any time to toggle the whole overlay on / off.
5. Right-click tray → **Settings...** to change color, size, HUD position, language, and more. All previews are live.

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

Output: `installer\MAXsCursor-Setup-v1.2.1.exe`.

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

## License

[MIT](LICENSE)

---

<a id="中文"></a>

## 为什么再写一个

市面上的光标 / 快捷键高亮工具（PointerFocus、Croncc、Carnac 等）在高刷屏上不是掉帧就是在 Maya 2026、UE5、Substance Designer 这种 GPU 满载的应用里出现撕裂。它们多数基于 WPF 或 Qt 的透明叠加窗口，这两条合成路径都打不过 DirectX 的 independent flip。

**MAXs Cursor** 把光标叠加做成**由低级钩子线程直接持有的原生 Win32 `WS_EX_LAYERED` 窗口**。位图通过 `UpdateLayeredWindow` 上传一次，之后的每次鼠标移动都在 `WH_MOUSE_LL` 回调里直接调 `SetWindowPos`，同线程同步。没有 UI 线程中转，没有 DWM 帧延迟，热路径上没有 WPF 渲染管线。在 Maya viewport 里 260 Hz 实测贴合。

## 功能

- **鼠标高亮环** — 跟随系统光标，近乎零延迟。颜色 / 半径 / 厚度 / 透明度全部实时调节。
- **快捷键 HUD** — 最近几次按键显示在鼠标所在显示器的底部中央。合并 chip：`Ctrl+Shift+S` 显示为一个而非三个。单独按下再松开的修饰键（`Alt`、`Ctrl` ...）也会作为自己的 chip 显示。
- **鼠标按键 chip** — `左键`、`右键`、`中键`、`滚轮↑ / ↓`、`Ctrl+左键` 等。独立开关。
- **多显示器** — HUD 自动跟随鼠标所在的屏幕。混合 DPI 用物理像素定位，不漂移。
- **全局开关** — `Alt+F5` 一键切换整个叠加层，任何应用前台都生效。
- **系统托盘** — 启用彩色图标，禁用灰度图标。右键菜单：启用 / 设置 / 退出。
- **设置窗口** — 实时预览、HSL 颜色选择、可拖拽的 HUD 位置选择、中 / 英文切换、跟随 Windows 深 / 浅色主题（WPF Fluent）。
- **零依赖** — 纯 .NET 10 + WPF + Win32 P/Invoke。无 MVVM 框架，无第三方 UI 库，无遥测，无网络调用。

## 下载

在 **[Releases](../../releases)** 页挑一个：

| 文件 | 类型 | 大小 | 适合 |
|---|---|---|---|
| `MAXsCursor.exe` | 绿色版（自包含） | 约 73 MB | 双击即用，拷到 U 盘也行，零安装 |
| `MAXsCursor-Setup-v1.2.1.exe` | 安装包（per-user，无需管理员） | 约 68 MB | 标准开始菜单入口 + Windows **应用与功能** 里可卸载 |

两者都内嵌 .NET 10 运行时，不用额外装任何东西。

## 使用

1. 双击 exe（或通过安装包的开始菜单启动）
2. 光标周围出现高亮环
3. 托盘出现彩色光标图标，右键打开菜单
4. 任何时候按 `Alt+F5` 切换整个叠加层显示 / 隐藏
5. 托盘右键 → **设置...** 改颜色、大小、HUD 位置、语言等，所有预览实时生效

## 从源码构建

需要 **.NET 10 SDK**。

```powershell
# Debug 构建
dotnet build src\MAXsCursor\MAXsCursor.csproj -c Debug

# 发布单文件 Release exe
dotnet publish src\MAXsCursor\MAXsCursor.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=embedded
```

发布产物在 `src\MAXsCursor\bin\Release\net10.0-windows\win-x64\publish\MAXsCursor.exe`。

**重建应用图标**（替换 `src\MAXsCursor\Assets\cursor.png` 之后）：

```powershell
.\make-icon.ps1
```

**打包安装器**（需先装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)）：

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\MAXsCursor.iss
```

输出 `installer\MAXsCursor-Setup-v1.2.1.exe`。

## 许可

[MIT](LICENSE)

<p align="center">
  <sub>Built for recording teaching videos. Made with care at 260 Hz.</sub><br/>
  <sub>为录制教学视频而生。在 260 Hz 下精心打磨。</sub>
</p>
