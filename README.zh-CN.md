<p align="center">
  <img src="assets/cursor.png" alt="MAXsCursor" width="160"/>
</p>

<h1 align="center">MAXs Cursor</h1>

<p align="center">
  <b>Windows 平台低延迟鼠标高亮 + 快捷键显示叠加工具。</b>
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
  <b><a href="README.md">English</a> | 中文</b>
</p>

---

## 为什么再写一个

市面上的光标 / 快捷键高亮工具（PointerFocus、Croncc、Carnac 等）在高刷屏上不是掉帧就是在 Maya 2026、UE5、Substance Designer 这种 GPU 满载的应用里出现撕裂。它们多数基于 WPF 或 Qt 的透明叠加窗口，这两条合成路径都打不过 DirectX 的 independent flip。

**MAXs Cursor** 把光标叠加做成**由低级钩子线程直接持有的原生 Win32 `WS_EX_LAYERED` 窗口**。位图通过 `UpdateLayeredWindow` 上传一次，之后的每次鼠标移动都在 `WH_MOUSE_LL` 回调里直接调 `SetWindowPos`，同线程同步。没有 UI 线程中转，没有 DWM 帧延迟，热路径上没有 WPF 渲染管线。在 Maya viewport 里 260 Hz 实测贴合。

---

## 功能

- **鼠标高亮环** — 跟随系统光标，近乎零延迟。颜色 / 半径 / 厚度 / 透明度全部实时调节。
- **快捷键 HUD** — 最近几次按键显示在鼠标所在显示器的底部中央。合并 chip：`Ctrl+Shift+S` 显示为一个而非三个。单独按下再松开的修饰键（`Alt`、`Ctrl` ...）也会作为自己的 chip 显示。
- **鼠标按键 chip** — `左键`、`右键`、`中键`、`滚轮↑ / ↓`、`Ctrl+左键` 等。独立开关。
- **多显示器** — HUD 自动跟随鼠标所在的屏幕。混合 DPI 用物理像素定位，不漂移。
- **全局开关** — `Alt+F5` 一键切换整个叠加层，任何应用前台都生效。
- **系统托盘** — 启用彩色图标，禁用灰度图标。右键菜单：启用 / 设置 / 退出。
- **设置窗口** — 实时预览、HSL 颜色选择、可拖拽的 HUD 位置选择、中 / 英文切换、跟随 Windows 深 / 浅色主题（WPF Fluent）。
- **零依赖** — 纯 .NET 10 + WPF + Win32 P/Invoke。无 MVVM 框架，无第三方 UI 库，无遥测，无网络调用。

---

## 下载

在 **[Releases](../../releases)** 页挑一个：

| 文件 | 类型 | 大小 | 适合 |
|---|---|---|---|
| `MAXsCursor.exe` | 绿色版（自包含） | 约 73 MB | 双击即用，拷到 U 盘也行，零安装 |
| `MAXsCursor-Setup-v1.0.exe` | 安装包（per-user，无需管理员） | 约 68 MB | 标准开始菜单入口 + Windows **应用与功能** 里可卸载 |

两者都内嵌 .NET 10 运行时，不用额外装任何东西。

---

## 使用

1. 双击 exe（或通过安装包的开始菜单启动）
2. 光标周围出现高亮环
3. 托盘出现彩色光标图标，右键打开菜单
4. 任何时候按 `Alt+F5` 切换整个叠加层显示 / 隐藏
5. 托盘右键 → **设置...** 改颜色、大小、HUD 位置、语言等，所有预览实时生效

---

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

输出 `installer\MAXsCursor-Setup-v1.0.exe`。

---

## 项目结构

```
src/MAXsCursor/
  Program.cs               程序入口 + 单实例 mutex
  App.xaml(.cs)            总调度：连接 hook、HUD、托盘、热键、设置
  Core/
    HookManager.cs         专用线程，持有 WH_MOUSE_LL + WH_KEYBOARD_LL + 光标窗口
    EventBus.cs            hook 线程 -> UI 线程的无锁队列
    HotkeyManager.cs       基于 message-only 窗口的 RegisterHotKey（Alt+F5）
    KeyTranslator.cs       VK + 修饰键 -> chip 文字
  Overlay/
    NativeCursorWindow.cs  纯 Win32 layered-window 叠加层，GDI+ 画到 DIB
    HudWindow.xaml(.cs)    WPF HUD 窗口，按显示器跟随
    KeyboardHudLayer.cs    chip 行，2 秒常显 + 0.6 秒淡出
    RenderClock.cs         CompositionTarget.Rendering 的封装
  Settings/
    SettingsModel.cs       POCO，System.Text.Json 往返序列化
    SettingsStore.cs       settings.json 的读写
    SettingsWindow.xaml    带实时预览的配置窗口
    HudPositionPicker.xaml 可拖拽的浮动面板，用于自定义 HUD 位置
    Strings.cs             中 / 英文文案表
  Tray/
    TrayIcon.cs            WinForms NotifyIcon 封装
  Interop/
    Win32.cs               所有 P/Invoke 声明
    WindowStyles.cs        WS_EX_* / SWP_* / WM_* 常量
```

设计理念和性能预算记录在 [SPEC.md](SPEC.md)。给贡献者（人或 AI）的工作规则在 [CLAUDE.md](CLAUDE.md)。

---

## 许可

[MIT](LICENSE)

<p align="center">
  <sub>为录制教学视频而生。在 260 Hz 下精心打磨。</sub>
</p>
