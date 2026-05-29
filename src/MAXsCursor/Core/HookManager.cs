using System.Collections.Concurrent;
using MAXsCursor.Interop;
using MAXsCursor.Overlay;

namespace MAXsCursor.Core;

// Dedicated hook thread that owns both WH_MOUSE_LL and WH_KEYBOARD_LL AND the
// native cursor window. Running the cursor on the hook thread is the critical
// optimisation for GPU-heavy apps like Maya 2026: the mouse-move callback can
// call SetWindowPos directly, no cross-thread marshaling, no UI-thread dependency,
// no WPF composition pipeline in the way.
//
// Keyboard stays on the WPF UI thread because the HUD uses WPF controls for fade
// animation; key rate is low and not a bottleneck.
internal sealed class HookManager : IDisposable
{
    private static EventBus? s_bus;
    private static Win32.HookProc? s_mouseProc;
    private static Win32.HookProc? s_keyboardProc;
    private static nint s_mouseHook;
    private static nint s_keyboardHook;
    private static readonly bool[] s_keyDown = new bool[256];

    // A modifier becomes "bare" on press and clears as soon as any non-modifier key
    // arrives while it's held. On release, still-bare modifiers get emitted as their
    // own chip ("Alt"/"Ctrl"/"Shift"/"Win").
    private static readonly bool[] s_bareModifier = new bool[256];

    // Direct mouse-move handler invoked from inside the hook callback on the hook
    // thread. Set after the cursor window is created on the same thread.
    private static Action<int, int>? s_onMouseMove;

    // Where to post WM_APP_INPUT_WAKE for non-mouse input (keyboard). The HUD window.
    private static nint s_wakeHwnd;
    public static void SetWakeHwnd(nint hwnd) => s_wakeHwnd = hwnd;

    private readonly EventBus _bus;
    private readonly ConcurrentQueue<Action> _tasks = new();
    private Thread? _thread;
    private uint _threadId;
    private readonly ManualResetEventSlim _ready = new(false);
    private volatile bool _running;
    private bool _disposed;

    // Cursor window lives on the hook thread.
    private NativeCursorWindow? _cursor;

    public HookManager(EventBus bus)
    {
        _bus = bus;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;

        _thread = new Thread(ThreadMain)
        {
            IsBackground = true,
            Name = "MAXsCursor.HookThread",
            Priority = ThreadPriority.AboveNormal
        };
        _thread.Start();
        _ready.Wait(TimeSpan.FromSeconds(2));
    }

    // Queue a task to run on the hook thread. Used by the UI thread to mutate the
    // cursor (settings changes, visibility toggles, teardown).
    public void RunOnHookThread(Action task)
    {
        _tasks.Enqueue(task);
        if (_threadId != 0)
        {
            Win32.PostThreadMessage(_threadId, (uint)Win32.WM_APP_HOOK_TASK, nint.Zero, nint.Zero);
        }
    }

    // High-level cursor operations. All marshal to hook thread.
    public void InitializeCursor(int sizeDip, double dpiScale, Action<NativeCursorWindow> configure)
    {
        RunOnHookThread(() =>
        {
            _cursor = new NativeCursorWindow(sizeDip, dpiScale);
            configure(_cursor);
            _cursor.SetVisible(true);
            // Wire the fast path: mouse hook callback will invoke this method group
            // directly, synchronously, on the hook thread.
            s_onMouseMove = _cursor.FollowCursor;

            if (Win32.GetCursorPos(out var pt))
            {
                _cursor.FollowCursor(pt.X, pt.Y);
            }
        });
    }

    public void ApplyCursorRing(byte r, byte g, byte b, double radius, double thickness, double opacity)
    {
        RunOnHookThread(() => _cursor?.ApplyRing(r, g, b, radius, thickness, opacity));
    }

    public void SetCursorVisible(bool visible)
    {
        RunOnHookThread(() => _cursor?.SetVisible(visible));
    }

    // Marshal a topmost re-assert to the hook thread that owns the cursor window.
    // Called periodically so other apps' topmost windows cannot bury the ring.
    public void ReassertCursorTopmost()
    {
        RunOnHookThread(() => _cursor?.ReassertTopmost());
    }

    private void ThreadMain()
    {
        try
        {
            s_bus = _bus;
            s_mouseProc = MouseHookProc;
            s_keyboardProc = KeyboardHookProc;

            var hMod = Win32.GetModuleHandle(null);
            s_mouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, s_mouseProc, hMod, 0);
            s_keyboardHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, s_keyboardProc, hMod, 0);

            _threadId = Win32.GetCurrentThreadId();
            _ready.Set();

            while (_running)
            {
                var ret = Win32.GetMessage(out var msg, nint.Zero, 0, 0);
                if (ret <= 0) break;

                if (msg.message == (uint)Win32.WM_APP_HOOK_TASK)
                {
                    while (_tasks.TryDequeue(out var task))
                    {
                        try { task(); } catch { /* hook thread must never crash */ }
                    }
                }

                Win32.TranslateMessage(ref msg);
                Win32.DispatchMessage(ref msg);
            }
        }
        finally
        {
            s_onMouseMove = null;
            _cursor?.Dispose();
            _cursor = null;
            if (s_mouseHook != nint.Zero) { Win32.UnhookWindowsHookEx(s_mouseHook); s_mouseHook = nint.Zero; }
            if (s_keyboardHook != nint.Zero) { Win32.UnhookWindowsHookEx(s_keyboardHook); s_keyboardHook = nint.Zero; }
            s_mouseProc = null;
            s_keyboardProc = null;
            s_bus = null;
        }
    }

    // Fast path: called on hook thread. No allocations, no cross-thread marshaling,
    // no WPF involvement. SetWindowPos on the cursor window is same-thread, microseconds.
    private static nint MouseHookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode == Win32.HC_ACTION)
        {
            var msg = wParam.ToInt32();
            unsafe
            {
                var data = *(Win32.MSLLHOOKSTRUCT*)lParam;

                if (msg == Win32.WM_MOUSEMOVE)
                {
                    var handler = s_onMouseMove;
                    handler?.Invoke(data.pt.X, data.pt.Y);
                }
                else
                {
                    var button = MapMouseMessage(msg, data.mouseData);
                    if (button != MouseButton.None)
                    {
                        // A click also counts as "using" any currently held modifier,
                        // so releasing Shift after Shift+Click does not emit a bare chip.
                        ClearHeldBareModifiers();
                        var mods = GetCurrentModifiers();
                        s_bus?.EnqueueMouseButton(new MouseButtonEvent(button, mods, data.time, data.pt.X, data.pt.Y));
                        WakeUi();
                    }
                }
            }
        }
        return Win32.CallNextHookEx(nint.Zero, nCode, wParam, lParam);
    }

    private static MouseButton MapMouseMessage(int msg, uint mouseData)
    {
        switch (msg)
        {
            case Win32.WM_LBUTTONDOWN: return MouseButton.Left;
            case Win32.WM_RBUTTONDOWN: return MouseButton.Right;
            case Win32.WM_MBUTTONDOWN: return MouseButton.Middle;
            case Win32.WM_XBUTTONDOWN:
                var xb = (mouseData >> 16) & 0xFFFF;
                return xb == 1 ? MouseButton.X1 : MouseButton.X2;
            case Win32.WM_MOUSEWHEEL:
                // High word is a signed 16-bit wheel delta. Positive = forward (up).
                var delta = (short)((mouseData >> 16) & 0xFFFF);
                return delta > 0 ? MouseButton.WheelUp : MouseButton.WheelDown;
            default:
                return MouseButton.None;
        }
    }

    private static nint KeyboardHookProc(int nCode, nint wParam, nint lParam)
    {
        if (nCode == Win32.HC_ACTION)
        {
            var msg = wParam.ToInt32();
            var isDown = msg == Win32.WM_KEYDOWN || msg == Win32.WM_SYSKEYDOWN;
            var isUp = msg == Win32.WM_KEYUP || msg == Win32.WM_SYSKEYUP;

            if (isDown || isUp)
            {
                unsafe
                {
                    var data = *(Win32.KBDLLHOOKSTRUCT*)lParam;
                    var vk = data.vkCode;
                    if (vk < 256)
                    {
                        var isMod = KeyTranslator.IsBareModifierKey(vk);

                        if (isUp)
                        {
                            // If this modifier was bare (never combined with a non-modifier),
                            // emit it as its own chip so the viewer sees the solo press.
                            if (isMod && s_bareModifier[vk])
                            {
                                s_bareModifier[vk] = false;
                                s_bus?.EnqueueKey(new KeyEvent(vk, ModifierMask.None, data.time));
                                WakeUi();
                            }
                            s_keyDown[vk] = false;
                        }
                        else if (!s_keyDown[vk])
                        {
                            s_keyDown[vk] = true;

                            if (isMod)
                            {
                                s_bareModifier[vk] = true;
                            }
                            else
                            {
                                // A non-modifier press consumes all currently held modifiers,
                                // so they won't also emit bare chips on their later release.
                                ClearHeldBareModifiers();
                                var mods = GetCurrentModifiers();
                                s_bus?.EnqueueKey(new KeyEvent(vk, mods, data.time));
                                WakeUi();
                            }
                        }
                    }
                }
            }
        }
        return Win32.CallNextHookEx(nint.Zero, nCode, wParam, lParam);
    }

    private static void WakeUi()
    {
        var target = s_wakeHwnd;
        if (target != nint.Zero)
        {
            Win32.PostMessage(target, (uint)Win32.WM_APP_INPUT_WAKE, nint.Zero, nint.Zero);
        }
    }

    private static void ClearHeldBareModifiers()
    {
        // Only the modifier VKs we emit chips for. Iterated explicitly for clarity and
        // to avoid a 256-wide loop in the hot path.
        ReadOnlySpan<int> vks = stackalloc int[]
        {
            Win32.VK_SHIFT, Win32.VK_LSHIFT, Win32.VK_RSHIFT,
            Win32.VK_CONTROL, Win32.VK_LCONTROL, Win32.VK_RCONTROL,
            Win32.VK_MENU, Win32.VK_LMENU, Win32.VK_RMENU,
            Win32.VK_LWIN, Win32.VK_RWIN
        };
        for (var i = 0; i < vks.Length; i++)
        {
            var v = vks[i];
            if (s_keyDown[v]) s_bareModifier[v] = false;
        }
    }

    private static ModifierMask GetCurrentModifiers()
    {
        var m = ModifierMask.None;
        if ((Win32.GetAsyncKeyState(Win32.VK_CONTROL) & 0x8000) != 0) m |= ModifierMask.Ctrl;
        if ((Win32.GetAsyncKeyState(Win32.VK_SHIFT) & 0x8000) != 0) m |= ModifierMask.Shift;
        if ((Win32.GetAsyncKeyState(Win32.VK_MENU) & 0x8000) != 0) m |= ModifierMask.Alt;
        if ((Win32.GetAsyncKeyState(Win32.VK_LWIN) & 0x8000) != 0
            || (Win32.GetAsyncKeyState(Win32.VK_RWIN) & 0x8000) != 0) m |= ModifierMask.Win;
        return m;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _running = false;

        if (_threadId != 0)
        {
            Win32.PostThreadMessage(_threadId, Win32.WM_QUIT, nint.Zero, nint.Zero);
        }
        _thread?.Join(TimeSpan.FromSeconds(1));
        _ready.Dispose();
    }
}
