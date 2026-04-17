using System.Windows.Media;

namespace MAXsCursor.Overlay;

// Wraps CompositionTarget.Rendering. Fires once per WPF composition frame on the UI thread,
// which is driven by the DWM vsync-ish cadence. This is the correct place to drain input
// events and update visuals. No Storyboard overhead, no timer drift.
internal sealed class RenderClock : IDisposable
{
    private readonly Action _onTick;
    private bool _running;

    public RenderClock(Action onTick)
    {
        _onTick = onTick;
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        CompositionTarget.Rendering += OnRendering;
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        CompositionTarget.Rendering -= OnRendering;
    }

    private void OnRendering(object? sender, EventArgs e) => _onTick();

    public void Dispose() => Stop();
}
