using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows;
using System.Windows.Resources;
using MAXsCursor.Settings;
using WinForms = System.Windows.Forms;

namespace MAXsCursor.Tray;

internal sealed class TrayIcon : IDisposable
{
    private readonly WinForms.NotifyIcon _icon;
    private readonly WinForms.ToolStripMenuItem _enableItem;
    private readonly WinForms.ToolStripMenuItem _settingsItem;
    private readonly WinForms.ToolStripMenuItem _quitItem;
    private readonly Action _onToggle;
    private readonly Action _onSettings;
    private readonly Action _onQuit;
    private readonly Icon _enabledIcon;
    private readonly Icon _disabledIcon;
    private bool _enabled = true;
    private bool _disposed;

    public TrayIcon(Action onToggle, Action onSettings, Action onQuit)
    {
        _onToggle = onToggle;
        _onSettings = onSettings;
        _onQuit = onQuit;

        (_enabledIcon, _disabledIcon) = LoadTrayIcons();

        var menu = new WinForms.ContextMenuStrip();
        _enableItem = new WinForms.ToolStripMenuItem(Strings.TrayDisable, null, (_, _) => _onToggle());
        _settingsItem = new WinForms.ToolStripMenuItem(Strings.TraySettings, null, (_, _) => _onSettings());
        _quitItem = new WinForms.ToolStripMenuItem(Strings.TrayQuit, null, (_, _) => _onQuit());
        menu.Items.Add(_enableItem);
        menu.Items.Add(_settingsItem);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(_quitItem);

        _icon = new WinForms.NotifyIcon
        {
            Icon = _enabledIcon,
            Text = Strings.TrayTooltipOn,
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => _onToggle();
    }

    public void SetEnabled(bool enabled)
    {
        _enabled = enabled;
        _icon.Icon = enabled ? _enabledIcon : _disabledIcon;
        _enableItem.Text = enabled ? Strings.TrayDisable : Strings.TrayEnable;
        _icon.Text = enabled ? Strings.TrayTooltipOn : Strings.TrayTooltipOff;
    }

    public void UpdateLanguage()
    {
        _enableItem.Text = _enabled ? Strings.TrayDisable : Strings.TrayEnable;
        _settingsItem.Text = Strings.TraySettings;
        _quitItem.Text = Strings.TrayQuit;
        _icon.Text = _enabled ? Strings.TrayTooltipOn : Strings.TrayTooltipOff;
    }

    // Loads the packaged Assets/cursor.png and produces two 32x32 tray icons:
    // colored when enabled, desaturated (grayscale) when disabled.
    private static (Icon enabled, Icon disabled) LoadTrayIcons()
    {
        var uri = new Uri("pack://application:,,,/Assets/cursor.png", UriKind.Absolute);
        StreamResourceInfo sri = System.Windows.Application.GetResourceStream(uri);
        using var stream = sri.Stream;
        using var source = new Bitmap(stream);

        var enabled = RenderAt(source, 32, desaturate: false);
        var disabled = RenderAt(source, 32, desaturate: true);
        return (enabled, disabled);
    }

    private static Icon RenderAt(Bitmap source, int size, bool desaturate)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.CompositingQuality = CompositingQuality.HighQuality;

            if (desaturate)
            {
                // ColorMatrix that maps RGB -> luminance for a grayscale disabled icon.
                var cm = new ColorMatrix(new[]
                {
                    new[] { 0.299f, 0.299f, 0.299f, 0f, 0f },
                    new[] { 0.587f, 0.587f, 0.587f, 0f, 0f },
                    new[] { 0.114f, 0.114f, 0.114f, 0f, 0f },
                    new[] { 0f,     0f,     0f,     0.6f, 0f },
                    new[] { 0f,     0f,     0f,     0f, 1f }
                });
                using var attrs = new ImageAttributes();
                attrs.SetColorMatrix(cm);
                var destRect = new Rectangle(0, 0, size, size);
                g.DrawImage(source, destRect, 0, 0, source.Width, source.Height, GraphicsUnit.Pixel, attrs);
            }
            else
            {
                g.DrawImage(source, 0, 0, size, size);
            }
        }

        var hIcon = bmp.GetHicon();
        using var temp = Icon.FromHandle(hIcon);
        return (Icon)temp.Clone();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _icon.Visible = false;
        _icon.Dispose();
        _enabledIcon.Dispose();
        _disabledIcon.Dispose();
    }
}
