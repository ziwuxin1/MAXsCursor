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
