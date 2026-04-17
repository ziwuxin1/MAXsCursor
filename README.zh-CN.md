<p align="center">
  <img src="assets/cursor.png" alt="MAXsCursor icon" width="96"/>
</p>

# MAXsCursor

**[English](README.md) | 中文**

Windows 平台低延迟鼠标高亮 + 快捷键显示叠加工具。为在 UE5、Maya、Substance Designer、Blender 等 GPU 高负载应用下录制 144 Hz / 240 Hz / 260 Hz 高刷教学视频而打造。

.NET 10 + WPF + 纯 Win32 layered window。无第三方 UI 框架，无遥测，无任何网络调用。

## 功能

- **鼠标高亮环** 跟随系统光标，近乎零延迟。颜色、半径、厚度、透明度全部支持实时调节。
- **快捷键 HUD** 显示在鼠标所在显示器的底部中间。最近几次按键以合并 chip 呈现：显示 `Ctrl+Shift+S` 而不是三个分开的键。单独按下再松开的修饰键（`Alt`、`Ctrl` ……）会作为自己的 chip 显示。
- **鼠标按键 chip**：`左键`、`右键`、`中键`、`滚轮↑ / ↓`、`Ctrl+左键` 等。独立开关。
- **多显示器支持**：HUD 自动跟随鼠标所在的屏幕。混合 DPI 场景用物理像素定位，不会错位。
- **全局开关**：`Alt+F5` 一键切换整个叠加层的显示，无论当前哪个应用在前台都生效。
- **系统托盘图标**：启用时彩色，禁用时灰度，区分明显。
- **设置窗口**：实时预览、HSL 颜色选择、HUD 位置自定义（拖动示例面板定位）、中/英文切换、跟随 Windows 深色 / 浅色主题（WPF Fluent）。
- **设置持久化** 到 `%APPDATA%\MAXsCursor\settings.json`。

## 为什么再写一个？

常见的 WPF 透明叠加工具在 Maya 2026 等 DirectX 应用中会掉帧，原因是它们走 WPF 自己的 layered-window 合成路径。MAXsCursor 把光标窗口做成 **由 hook 线程直接拥有的原生 Win32 `WS_EX_LAYERED` 窗口**：位图只通过 `UpdateLayeredWindow` 上传一次，后续移动由 `WH_MOUSE_LL` 回调直接调用 `SetWindowPos`。没有 UI 线程中转、没有 DWM 帧延迟。和 PointerFocus 这类商业工具一个路线，用 C# 干净复刻。

## 下载

进入 [Releases](../../releases) 页获取：

- **MAXsCursor.exe** — 绿色版单文件（约 73 MB，已打包 .NET 10 运行时），双击即用，免安装
- **MAXsCursor-Setup-v1.0.exe** — 带卸载程序的安装包（约 68 MB），装到 `%LOCALAPPDATA%\Programs\MAXsCursor`，不需要管理员权限

两个都可以，功能完全一致。装系统里想走标准卸载流程选安装包；想即拷即用或多台机器便携选单文件版。

## 从源码构建

需要 .NET 10 SDK。仓库根目录下：

```powershell
dotnet build src\MAXsCursor\MAXsCursor.csproj -c Debug
# 或者发布单文件 release exe：
dotnet publish src\MAXsCursor\MAXsCursor.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true -p:DebugType=embedded
```

发布产物在 `src\MAXsCursor\bin\Release\net10.0-windows\win-x64\publish\MAXsCursor.exe`。

换图标：替换 `src\MAXsCursor\Assets\cursor.png`，然后跑：

```powershell
.\make-icon.ps1
```

重新生成多尺寸 `.ico`。

打包安装器（需要先装 [Inno Setup 6](https://jrsoftware.org/isinfo.php)）：

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" installer\MAXsCursor.iss
```

输出在 `installer\MAXsCursor-Setup-v1.0.exe`。

## 项目结构

```
src/MAXsCursor/
  Program.cs               程序入口 + 单实例 mutex
  App.xaml(.cs)            总调度：连接 hook、HUD、托盘、热键、设置
  Core/
    HookManager.cs         专用线程，持有 WH_MOUSE_LL + WH_KEYBOARD_LL + 光标窗口
    EventBus.cs            hook 线程 -> UI 线程的无锁队列
    HotkeyManager.cs       基于 message-only 窗口的 RegisterHotKey 实现（Alt+F5）
    KeyTranslator.cs       VK + 修饰键 -> HUD chip 文字
  Overlay/
    NativeCursorWindow.cs  纯 Win32 layered-window 叠加层，GDI+ 画到 DIB
    HudWindow.xaml(.cs)    WPF HUD 窗口，按显示器跟随
    KeyboardHudLayer.cs    chip 行，2 秒常显 + 0.6 秒线性淡出
    RenderClock.cs         CompositionTarget.Rendering 的封装
  Settings/
    SettingsModel.cs       POCO；System.Text.Json 往返序列化
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

设计理念和性能约束记录在 [SPEC.md](SPEC.md)。给 Claude Code 和人类贡献者的工作规则在 [CLAUDE.md](CLAUDE.md)。

## 许可

MIT，详见 [LICENSE](LICENSE)。
