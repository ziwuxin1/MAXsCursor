using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using MAXsCursor.Interop;

namespace MAXsCursor.Settings;

// Modal picker. Shows a draggable sample chip; user drags to the desired spot,
// clicks 确定, and we hand back the window's physical-pixel position. HudWindow
// uses that as its pinned location.
internal partial class HudPositionPicker : Window
{
    private readonly int _startX;
    private readonly int _startY;
    private readonly double _fontSize;
    private nint _hwnd;

    public bool Confirmed { get; private set; }
    public int ResultX { get; private set; }
    public int ResultY { get; private set; }

    public HudPositionPicker(int startX, int startY, double fontSize)
    {
        InitializeComponent();
        _startX = startX;
        _startY = startY;
        _fontSize = fontSize;
        SampleText.FontSize = fontSize;

        Title = Strings.PickerTitle;
        HintText.Text = Strings.DragHint;
        CancelButton.Content = Strings.Cancel;
        OkButton.Content = Strings.Ok;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        _hwnd = new WindowInteropHelper(this).Handle;

        // Place the picker at the requested starting position in physical pixels,
        // bypassing WPF's DIP transforms so PerMonitor DPI boundaries do not shift
        // the picker away from where it was previously confirmed.
        const uint flags = WindowStyles.SWP_NOSIZE | WindowStyles.SWP_NOACTIVATE | WindowStyles.SWP_NOZORDER;
        Win32.SetWindowPos(_hwnd, nint.Zero, _startX, _startY, 0, 0, flags);
    }

    private void OnDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            try { DragMove(); } catch { /* DragMove can throw if the mouse is released mid-call */ }
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (Win32.GetWindowRect(_hwnd, out var r))
        {
            ResultX = r.Left;
            ResultY = r.Top;
            Confirmed = true;
        }
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }
}
