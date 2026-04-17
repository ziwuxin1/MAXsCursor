using System.IO;
using MAXsCursor.Core;
using MAXsCursor.Interop;
using MAXsCursor.Overlay;
using MAXsCursor.Settings;
using MAXsCursor.Tray;
using Application = System.Windows.Application;
using StartupEventArgs = System.Windows.StartupEventArgs;
using ExitEventArgs = System.Windows.ExitEventArgs;

namespace MAXsCursor;

public partial class App : Application
{
    private HudWindow? _hud;
    private TrayIcon? _tray;
    private HotkeyManager? _hotkey;
    private HookManager? _hook;
    private RenderClock? _clock;
    private EventBus? _bus;
    private SettingsWindow? _settingsWindow;
    private SettingsModel _settings = SettingsModel.Defaults();
    private bool _enabled = true;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try { Win32.SetProcessDpiAwarenessContext(Win32.DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
        catch { }

        _settings = SettingsStore.Load();
        Strings.SetLanguage(_settings.Language);

        _hud = new HudWindow();
        _hud.Show();
        _hud.ApplyFontSize(_settings.HudFontSize);
        _hud.ApplyCustomPosition(_settings.HudCustomPosition, _settings.HudX, _settings.HudY);
        _hud.SetHudVisible(_settings.HudEnabled);
        _hud.InputWake += OnInputWake;

        _bus = new EventBus();
        _hook = new HookManager(_bus);
        HookManager.SetWakeHwnd(_hud.Handle);
        _hook.Start();

        // Cursor window lives on the hook thread. Hand over initial ring config via a
        // closure snapshot of settings; the hook thread will render the bitmap once.
        var dpiScale = DetectDpiScale();
        var initial = _settings.Clone();
        _hook.InitializeCursor(sizeDip: 220, dpiScale: dpiScale, configure: cursor =>
        {
            var rgb = ColorParse.Parse(initial.RingColor);
            cursor.ApplyRing(rgb.R, rgb.G, rgb.B,
                initial.RingRadius, initial.RingThickness, initial.RingOpacity);
        });

        // Render clock drives HUD fade only. Cursor is driven directly from the hook
        // callback on the hook thread, bypassing the UI-thread cadence entirely.
        _clock = new RenderClock(OnFrameTick);
        _clock.Start();

        _hotkey = new HotkeyManager(ToggleEnabled);
        if (!_hotkey.Register())
        {
            Log("WARN: Alt+F5 hotkey registration failed.");
        }

        _tray = new TrayIcon(onToggle: ToggleEnabled, onSettings: ShowSettings, onQuit: ShutdownCleanly);
        _tray.SetEnabled(_enabled);

        AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupHooks();
    }

    private static double DetectDpiScale()
    {
        var screen = Win32.GetDC(nint.Zero);
        try
        {
            var dx = GetDeviceCaps(screen, LOGPIXELSX);
            return dx / 96.0;
        }
        finally
        {
            Win32.ReleaseDC(nint.Zero, screen);
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(nint hdc, int nIndex);
    private const int LOGPIXELSX = 88;

    private void OnInputWake()
    {
        // Keyboard and mouse-button chips both feed the HUD on the UI thread.
        // Mouse-move is already handled on the hook thread, bypassing this.
        DrainKeysNow();
        DrainMouseButtonsNow();
    }

    private void OnFrameTick()
    {
        DrainKeysNow();
        DrainMouseButtonsNow();
        _hud?.TickHud();
    }

    private void DrainKeysNow()
    {
        if (_bus is null || _hud is null) return;
        var pushed = false;
        while (_bus.TryDequeueKey(out var key))
        {
            var text = KeyTranslator.ToDisplayText(key.VkCode, key.Modifiers);
            if (text is not null)
            {
                _hud.PushKeyChip(text);
                pushed = true;
            }
        }
        if (pushed)
        {
            _hud.RepositionToCursorMonitor();
        }
    }

    private void DrainMouseButtonsNow()
    {
        if (_bus is null || _hud is null) return;

        var pushed = false;
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
        if (pushed)
        {
            _hud.RepositionToCursorMonitor();
        }
    }

    private void ApplyCursorSettings()
    {
        if (_hook is null) return;
        var rgb = ColorParse.Parse(_settings.RingColor);
        _hook.ApplyCursorRing(rgb.R, rgb.G, rgb.B,
            _settings.RingRadius, _settings.RingThickness, _settings.RingOpacity);
    }

    private void ShowSettings()
    {
        Dispatcher.Invoke(() =>
        {
            if (_settingsWindow is { IsLoaded: true })
            {
                _settingsWindow.Activate();
                return;
            }
            _settingsWindow = new SettingsWindow(_settings, OnSettingsChanged);
            _settingsWindow.Closed += (_, _) =>
            {
                SettingsStore.Save(_settings);
                _settingsWindow = null;
            };
            _settingsWindow.Show();
        });
    }

    private void OnSettingsChanged(SettingsModel updated)
    {
        var languageChanged = !string.Equals(_settings.Language, updated.Language, StringComparison.OrdinalIgnoreCase);
        _settings = updated.Clone();

        if (languageChanged)
        {
            Strings.SetLanguage(_settings.Language);
            _tray?.UpdateLanguage();
        }

        ApplyCursorSettings();
        if (_hud is not null)
        {
            _hud.ApplyFontSize(_settings.HudFontSize);
            _hud.ApplyCustomPosition(_settings.HudCustomPosition, _settings.HudX, _settings.HudY);
            _hud.SetHudVisible(_enabled && _settings.HudEnabled);
            if (!_settings.HudEnabled) _hud.ClearHud();
        }
    }

    private void ToggleEnabled()
    {
        Dispatcher.Invoke(() =>
        {
            _enabled = !_enabled;
            _hook?.SetCursorVisible(_enabled);
            _hud?.SetHudVisible(_enabled && _settings.HudEnabled);
            _tray?.SetEnabled(_enabled);
            if (!_enabled) _hud?.ClearHud();
        });
    }

    private void ShutdownCleanly()
    {
        Dispatcher.Invoke(() =>
        {
            SettingsStore.Save(_settings);
            CleanupHooks();
            Shutdown();
        });
    }

    private void CleanupHooks()
    {
        _clock?.Dispose();
        _clock = null;
        _hook?.Dispose();
        _hook = null;
        _hotkey?.Dispose();
        _hotkey = null;
        _tray?.Dispose();
        _tray = null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        SettingsStore.Save(_settings);
        CleanupHooks();
        base.OnExit(e);
    }

    private static void Log(string message)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), "MAXsCursor.log");
            File.AppendAllText(path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch { }
    }
}
