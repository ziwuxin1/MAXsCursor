using System.Windows;
using System.Windows.Controls;
using Brushes = System.Windows.Media.Brushes;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;
using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace MAXsCursor.Overlay;

// Bottom-right chip row showing the last few key combos.
// Each chip is fully opaque for VisibleSeconds, then linearly fades over FadeSeconds
// and gets removed. Tick() is called once per frame from the render clock.
internal sealed class KeyboardHudLayer : StackPanel
{
    private const int MaxChips = 5;
    private const double VisibleSeconds = 2.0;
    private const double FadeSeconds = 0.6;

    private double _fontSize = 20.0;

    private readonly List<ChipEntry> _chips = new(MaxChips + 1);

    private readonly struct ChipEntry
    {
        public readonly Border Border;
        public readonly long TickStampMs;
        public ChipEntry(Border b, long ts) { Border = b; TickStampMs = ts; }
    }

    public KeyboardHudLayer()
    {
        Orientation = System.Windows.Controls.Orientation.Horizontal;
        IsHitTestVisible = false;
    }

    public void PushKey(string text)
    {
        var chip = BuildChip(text);
        Children.Add(chip);
        _chips.Add(new ChipEntry(chip, Environment.TickCount64));

        while (_chips.Count > MaxChips)
        {
            Children.Remove(_chips[0].Border);
            _chips.RemoveAt(0);
        }
    }

    public void Tick()
    {
        if (_chips.Count == 0) return;

        var now = Environment.TickCount64;
        for (var i = _chips.Count - 1; i >= 0; i--)
        {
            var entry = _chips[i];
            var ageSec = (now - entry.TickStampMs) / 1000.0;

            if (ageSec < VisibleSeconds)
            {
                if (entry.Border.Opacity != 1.0) entry.Border.Opacity = 1.0;
                continue;
            }

            var fadeT = (ageSec - VisibleSeconds) / FadeSeconds;
            if (fadeT >= 1.0)
            {
                Children.Remove(entry.Border);
                _chips.RemoveAt(i);
            }
            else
            {
                entry.Border.Opacity = 1.0 - fadeT;
            }
        }
    }

    public void Clear()
    {
        Children.Clear();
        _chips.Clear();
    }

    public void SetFontSize(double size)
    {
        _fontSize = Math.Clamp(size, 10.0, 64.0);
    }

    private Border BuildChip(string text)
    {
        var tb = new TextBlock
        {
            Text = text,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = _fontSize,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White,
            VerticalAlignment = System.Windows.VerticalAlignment.Center,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Center
        };
        return new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x10, 0x10, 0x10)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x60, 0xFF, 0xFF, 0xFF)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(14, 6, 14, 6),
            Margin = new Thickness(4, 0, 4, 0),
            Child = tb,
            IsHitTestVisible = false
        };
    }
}
