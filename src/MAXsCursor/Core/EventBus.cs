using System.Collections.Concurrent;

namespace MAXsCursor.Core;

internal readonly record struct MouseMoveEvent(int X, int Y, long TimestampTicks);

[Flags]
internal enum ModifierMask : uint
{
    None = 0,
    Ctrl = 1,
    Shift = 2,
    Alt = 4,
    Win = 8
}

internal readonly record struct KeyEvent(uint VkCode, ModifierMask Modifiers, long TimestampTicks);

internal enum MouseButton : byte
{
    None, Left, Right, Middle, X1, X2, WheelUp, WheelDown
}

internal readonly record struct MouseButtonEvent(MouseButton Button, ModifierMask Modifiers, long TimestampTicks, int X, int Y);

// Hook-thread to UI-thread handoff. ConcurrentQueue is allocation-free for value-type
// enqueue/dequeue, which matches the zero-alloc contract for hook callbacks.
internal sealed class EventBus
{
    private readonly ConcurrentQueue<MouseMoveEvent> _mouseMoves = new();
    private readonly ConcurrentQueue<KeyEvent> _keys = new();
    private readonly ConcurrentQueue<MouseButtonEvent> _mouseButtons = new();

    public void EnqueueMouseMove(in MouseMoveEvent e) => _mouseMoves.Enqueue(e);
    public void EnqueueKey(in KeyEvent e) => _keys.Enqueue(e);
    public void EnqueueMouseButton(in MouseButtonEvent e) => _mouseButtons.Enqueue(e);

    // Cursor tracks the latest position; intermediate moves are stale for a ring
    // that just follows the cursor, so coalesce by returning the last one only.
    public bool TryDrainLatestMouseMove(out MouseMoveEvent latest)
    {
        latest = default;
        var any = false;
        while (_mouseMoves.TryDequeue(out var e))
        {
            latest = e;
            any = true;
        }
        return any;
    }

    // Keys cannot be coalesced: every keypress is meaningful to the HUD.
    public bool TryDequeueKey(out KeyEvent key) => _keys.TryDequeue(out key);

    public bool TryDequeueMouseButton(out MouseButtonEvent ev) => _mouseButtons.TryDequeue(out ev);
}
