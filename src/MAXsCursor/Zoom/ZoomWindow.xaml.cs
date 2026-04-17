using System.Windows;
using System.Windows.Ink;
using InkCanvasEditingMode = System.Windows.Controls.InkCanvasEditingMode;
using RectangleStylusShape = System.Windows.Ink.RectangleStylusShape;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MAXsCursor.Interop;
using MAXsCursor.Settings;
using Button = System.Windows.Controls.Button;
using Canvas = System.Windows.Controls.Canvas;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using MouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;
using MouseWheelEventArgs = System.Windows.Input.MouseWheelEventArgs;
using Keyboard = System.Windows.Input.Keyboard;
using Key = System.Windows.Input.Key;
using MouseButton = System.Windows.Input.MouseButton;
using ModifierKeys = System.Windows.Input.ModifierKeys;
using Cursors = System.Windows.Input.Cursors;
using System.IO;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.SolidColorBrush;
using ColorConverter = System.Windows.Media.ColorConverter;
using DrawingVisual = System.Windows.Media.DrawingVisual;
using RenderTargetBitmap = System.Windows.Media.Imaging.RenderTargetBitmap;
using PngBitmapEncoder = System.Windows.Media.Imaging.PngBitmapEncoder;
using BitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using PixelFormats = System.Windows.Media.PixelFormats;
using Clipboard = System.Windows.Clipboard;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace MAXsCursor.Zoom;

// Full-screen "freeze frame + zoom + draw" window, modelled after Sysinternals ZoomIt.
// On open:
//   - Captures the monitor under the cursor via GDI BitBlt.
//   - Shows the capture at 2x zoom centered on the cursor.
//   - InkCanvas collects pen strokes on top of the zoomed capture.
//   - Scroll wheel changes zoom (1x..5x). Middle-mouse / spacebar+drag pans the view.
//   - Esc closes the window.
internal partial class ZoomWindow : Window
{
    private readonly BitmapSource _screen;
    private readonly int _screenPxWidth;
    private readonly int _screenPxHeight;
    private readonly int _monitorLeftPx;
    private readonly int _monitorTopPx;

    private readonly ScaleTransform _scale = new(2.0, 2.0);
    private readonly TranslateTransform _translate = new();

    private const double MinScale = 1.0;
    private const double MaxScale = 5.0;

    private bool _panning;
    private System.Windows.Point _panStart;
    private double _panStartTx;
    private double _panStartTy;

    private readonly Stack<Stroke> _redoStack = new();

    public ZoomWindow()
    {
        // Capture BEFORE InitializeComponent so the window does not appear in its own screenshot.
        var capture = ScreenCapture.CaptureCursorMonitor();
        _screen = capture.image;
        _screenPxWidth = capture.width;
        _screenPxHeight = capture.height;
        _monitorLeftPx = capture.left;
        _monitorTopPx = capture.top;

        InitializeComponent();

        // Size this WPF window to cover exactly the captured monitor, in physical pixels.
        // PerMonitorV2 aware -> WPF will pick the monitor's DPI for Left/Top/Width/Height.
        // We set position via Win32 SetWindowPos to avoid DIP round-trip issues.
        Left = _monitorLeftPx;
        Top = _monitorTopPx;
        Width = _screenPxWidth;
        Height = _screenPxHeight;

        // Paint the captured screenshot at native pixel size onto the content canvas.
        ScreenImage.Source = _screen;
        ScreenImage.Width = _screenPxWidth;
        ScreenImage.Height = _screenPxHeight;
        Canvas.SetLeft(ScreenImage, 0);
        Canvas.SetTop(ScreenImage, 0);

        // Ink canvas covers the exact same area so stroke coordinates match the screenshot.
        Ink.Width = _screenPxWidth;
        Ink.Height = _screenPxHeight;
        Canvas.SetLeft(Ink, 0);
        Canvas.SetTop(Ink, 0);

        // Compose scale + translate into the canvas. Translate first so scaling does not
        // multiply the translation offset.
        var tg = new TransformGroup();
        tg.Children.Add(_scale);
        tg.Children.Add(_translate);
        ContentCanvas.RenderTransform = tg;

        // Initial color = red, initial thickness from slider default.
        SetInkColor(MediaColor.FromRgb(0xFF, 0x3B, 0x30));
        Ink.DefaultDrawingAttributes.Width = ThicknessSlider.Value;
        Ink.DefaultDrawingAttributes.Height = ThicknessSlider.Value;
        Ink.DefaultDrawingAttributes.FitToCurve = true;

        ApplySwatchVisuals();
        ApplyToolVisuals();
        ApplyStrings();

        Loaded += OnLoaded;
        // PreviewKeyDown reaches us before InkCanvas can mark the event Handled,
        // so shortcuts like Esc still trigger even while the user is drawing.
        PreviewKeyDown += OnKeyDown;
        PreviewMouseWheel += OnMouseWheel;
        PreviewMouseDown += OnPreviewMouseDown;
        PreviewMouseMove += OnPreviewMouseMove;
        PreviewMouseUp += OnPreviewMouseUp;

        // Keep track of strokes so we can implement Undo.
        Ink.Strokes.StrokesChanged += (_, args) =>
        {
            if (args.Added.Count > 0) _redoStack.Clear();
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Activate();
        Focus();
        Keyboard.Focus(this);

        // Center the initial view on the cursor position.
        if (Win32.GetCursorPos(out var pt))
        {
            var localX = pt.X - _monitorLeftPx;
            var localY = pt.Y - _monitorTopPx;
            CenterOn(localX, localY);
        }
    }

    // Places (cx, cy) in screenshot space at the window center by adjusting the translate.
    private void CenterOn(double cx, double cy)
    {
        _translate.X = (ActualWidth / 2) - cx * _scale.ScaleX;
        _translate.Y = (ActualHeight / 2) - cy * _scale.ScaleY;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                return;
            case Key.D1: case Key.NumPad1: PickSwatch(ColorRed); e.Handled = true; return;
            case Key.D2: case Key.NumPad2: PickSwatch(ColorOrange); e.Handled = true; return;
            case Key.D3: case Key.NumPad3: PickSwatch(ColorYellow); e.Handled = true; return;
            case Key.D4: case Key.NumPad4: PickSwatch(ColorGreen); e.Handled = true; return;
            case Key.D5: case Key.NumPad5: PickSwatch(ColorBlue); e.Handled = true; return;
            case Key.D6: case Key.NumPad6: PickSwatch(ColorPink); e.Handled = true; return;
            case Key.D7: case Key.NumPad7: PickSwatch(ColorWhite); e.Handled = true; return;
            case Key.OemPlus: case Key.Add: ThicknessSlider.Value = Math.Min(ThicknessSlider.Maximum, ThicknessSlider.Value + 1); e.Handled = true; return;
            case Key.OemMinus: case Key.Subtract: ThicknessSlider.Value = Math.Max(ThicknessSlider.Minimum, ThicknessSlider.Value - 1); e.Handled = true; return;
            case Key.B: SetMode(InkCanvasEditingMode.Ink); e.Handled = true; return;
            case Key.E: SetMode(InkCanvasEditingMode.EraseByPoint); e.Handled = true; return;
            case Key.C:
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                    OnCopy(this, new RoutedEventArgs());
                else
                    OnClear(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            case Key.S:
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    OnSave(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                return;
            case Key.Z:
                if ((Keyboard.Modifiers & ModifierKeys.Control) != 0)
                {
                    OnUndo(this, new RoutedEventArgs());
                    e.Handled = true;
                }
                return;
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Zoom around the cursor position so the pixel under the pointer stays put.
        var pos = e.GetPosition(this);
        var beforeX = (pos.X - _translate.X) / _scale.ScaleX;
        var beforeY = (pos.Y - _translate.Y) / _scale.ScaleY;

        var factor = e.Delta > 0 ? 1.15 : 1.0 / 1.15;
        var newScale = Math.Clamp(_scale.ScaleX * factor, MinScale, MaxScale);

        _scale.ScaleX = newScale;
        _scale.ScaleY = newScale;

        _translate.X = pos.X - beforeX * newScale;
        _translate.Y = pos.Y - beforeY * newScale;

        e.Handled = true;
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        // Middle-button OR Space+left triggers pan. Left-alone keeps drawing via InkCanvas.
        var wantsPan = e.ChangedButton == MouseButton.Middle
                       || (e.ChangedButton == MouseButton.Left && Keyboard.IsKeyDown(Key.Space));
        if (wantsPan)
        {
            _panning = true;
            _panStart = e.GetPosition(this);
            _panStartTx = _translate.X;
            _panStartTy = _translate.Y;
            CaptureMouse();
            Cursor = Cursors.SizeAll;
            e.Handled = true;
        }
    }

    private void OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        // Eraser visual preview follows the cursor in ink-canvas space. Placing it in
        // the content canvas means the scale transform stretches it exactly like the
        // real eraser footprint, so what you see is what gets erased.
        if (EraserPreview.Visibility == Visibility.Visible)
        {
            var p = e.GetPosition(Ink);
            System.Windows.Controls.Canvas.SetLeft(EraserPreview, p.X - EraserPreview.Width / 2);
            System.Windows.Controls.Canvas.SetTop(EraserPreview, p.Y - EraserPreview.Height / 2);
        }

        if (!_panning) return;
        var now = e.GetPosition(this);
        _translate.X = _panStartTx + (now.X - _panStart.X);
        _translate.Y = _panStartTy + (now.Y - _panStart.Y);
    }

    private void OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_panning)
        {
            _panning = false;
            ReleaseMouseCapture();
            Cursor = Cursors.Cross;
            e.Handled = true;
        }
    }

    private void OnColorClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is string hex)
        {
            PickSwatch(b);
            SetMode(InkCanvasEditingMode.Ink);
        }
    }

    private void OnPenClick(object sender, RoutedEventArgs e) => SetMode(InkCanvasEditingMode.Ink);

    private void OnEraserClick(object sender, RoutedEventArgs e) => SetMode(InkCanvasEditingMode.EraseByPoint);

    private void SetMode(InkCanvasEditingMode mode)
    {
        // Eraser needs an InkCanvas "reset" to pick up a newly assigned EraserShape,
        // because InkCanvas caches the current stylus shape on mode entry.
        Ink.EditingMode = InkCanvasEditingMode.None;
        var w = ComputeEraserSize();
        Ink.EraserShape = new RectangleStylusShape(w, w);
        Ink.EditingMode = mode;
        UpdateEraserPreviewSize();
        ApplyToolVisuals();
    }

    private double ComputeEraserSize() => Math.Max(4, ThicknessSlider.Value * 2);

    private void UpdateEraserPreviewSize()
    {
        var w = ComputeEraserSize();
        EraserPreview.Width = w;
        EraserPreview.Height = w;
    }

    private void ApplyToolVisuals()
    {
        var isEraser = Ink.EditingMode == InkCanvasEditingMode.EraseByPoint
                    || Ink.EditingMode == InkCanvasEditingMode.EraseByStroke;

        PenButton.Background = new MediaBrush(isEraser
            ? MediaColor.FromArgb(0x30, 0xFF, 0xFF, 0xFF)
            : MediaColor.FromArgb(0x80, 0xFF, 0xFF, 0xFF));
        EraserButton.Background = new MediaBrush(!isEraser
            ? MediaColor.FromArgb(0x30, 0xFF, 0xFF, 0xFF)
            : MediaColor.FromArgb(0x80, 0xFF, 0xFF, 0xFF));

        // Eraser: hide the crosshair, show a dashed preview that matches the real erase footprint.
        if (isEraser)
        {
            Cursor = Cursors.None;
            EraserPreview.Visibility = Visibility.Visible;
        }
        else
        {
            Cursor = Cursors.Cross;
            EraserPreview.Visibility = Visibility.Collapsed;
        }
    }

    private void PickSwatch(Button b)
    {
        if (b.Tag is not string hex) return;
        var c = (MediaColor)ColorConverter.ConvertFromString(hex);
        SetInkColor(c);
        ApplySwatchVisuals();
    }

    private void SetInkColor(MediaColor c)
    {
        Ink.DefaultDrawingAttributes.Color = c;
        _currentColor = c;
    }

    private MediaColor _currentColor = MediaColor.FromRgb(0xFF, 0x3B, 0x30);

    private void ApplySwatchVisuals()
    {
        foreach (var btn in new[] { ColorRed, ColorOrange, ColorYellow, ColorGreen, ColorBlue, ColorPink, ColorWhite })
        {
            if (btn.Tag is not string hex) continue;
            var c = (MediaColor)ColorConverter.ConvertFromString(hex);
            var isSelected = c == _currentColor;
            btn.Background = new MediaBrush(c);
            btn.BorderBrush = new MediaBrush(isSelected
                ? MediaColor.FromArgb(0xFF, 0xFF, 0xFF, 0xFF)
                : MediaColor.FromArgb(0x60, 0xFF, 0xFF, 0xFF));
            btn.BorderThickness = new Thickness(isSelected ? 2 : 1);
        }
    }

    private void OnThicknessChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e)
    {
        Ink.DefaultDrawingAttributes.Width = e.NewValue;
        Ink.DefaultDrawingAttributes.Height = e.NewValue;
        // If the user is mid-eraser, we need to bounce the EditingMode so InkCanvas
        // picks up the new EraserShape; property changes alone are not enough.
        var w = ComputeEraserSize();
        if (Ink != null)
        {
            if (Ink.EditingMode == InkCanvasEditingMode.EraseByPoint || Ink.EditingMode == InkCanvasEditingMode.EraseByStroke)
            {
                var saved = Ink.EditingMode;
                Ink.EditingMode = InkCanvasEditingMode.None;
                Ink.EraserShape = new RectangleStylusShape(w, w);
                Ink.EditingMode = saved;
            }
            else
            {
                Ink.EraserShape = new RectangleStylusShape(w, w);
            }
        }
        if (EraserPreview != null)
        {
            EraserPreview.Width = w;
            EraserPreview.Height = w;
        }
        if (ThicknessValueText != null) ThicknessValueText.Text = $"{e.NewValue:F0} px";
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        Ink.Strokes.Clear();
        _redoStack.Clear();
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        if (Ink.Strokes.Count == 0) return;
        var last = Ink.Strokes[Ink.Strokes.Count - 1];
        Ink.Strokes.Remove(last);
        _redoStack.Push(last);
    }

    private void OnExit(object sender, RoutedEventArgs e) => Close();

    // Render the captured screen + ink strokes into an image at the original monitor
    // resolution. Toolbar and preview ellipse are not part of the content canvas's
    // ink tree, so they never leak into the export.
    private RenderTargetBitmap ComposeOutput()
    {
        var rtb = new RenderTargetBitmap(_screenPxWidth, _screenPxHeight, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawImage(_screen, new Rect(0, 0, _screenPxWidth, _screenPxHeight));
            foreach (var stroke in Ink.Strokes)
            {
                stroke.Draw(dc);
            }
        }
        rtb.Render(dv);
        rtb.Freeze();
        return rtb;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Filter = "PNG image (*.png)|*.png",
            FileName = $"MAXsCursor-{DateTime.Now:yyyyMMdd-HHmmss}.png",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            AddExtension = true,
            DefaultExt = ".png",
            OverwritePrompt = true
        };
        if (dlg.ShowDialog(this) != true) return;

        try
        {
            var bmp = ComposeOutput();
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = File.Create(dlg.FileName);
            enc.Save(fs);
            ShowToast(Strings.ZoomSaved);
        }
        catch (Exception ex)
        {
            ShowToast($"{Strings.ZoomSaveFailed}: {ex.Message}");
        }
    }

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            var bmp = ComposeOutput();
            Clipboard.SetImage(bmp);
            ShowToast(Strings.ZoomCopied);
        }
        catch (Exception ex)
        {
            ShowToast($"{Strings.ZoomCopyFailed}: {ex.Message}");
        }
    }

    // Tiny transient message in the hint box, auto-clears after ~2 s.
    private System.Windows.Threading.DispatcherTimer? _toastTimer;
    private void ShowToast(string message)
    {
        HintText.Text = message;
        _toastTimer?.Stop();
        _toastTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer?.Stop();
            HintText.Text = Strings.ZoomHint;
        };
        _toastTimer.Start();
    }

    private void ApplyStrings()
    {
        Title = Strings.ZoomTitle;
        ThicknessLabelText.Text = Strings.ZoomThickness;
        PenButton.Content = Strings.ZoomPen;
        EraserButton.Content = Strings.ZoomEraser;
        ClearButton.Content = Strings.ZoomClear;
        SaveButton.Content = Strings.ZoomSave;
        CopyButton.Content = Strings.ZoomCopy;
        ExitButton.Content = Strings.ZoomExit;
        HintText.Text = Strings.ZoomHint;
        if (ThicknessValueText != null) ThicknessValueText.Text = $"{ThicknessSlider.Value:F0} px";
    }
}
