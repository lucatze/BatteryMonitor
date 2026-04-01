using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows;

namespace BatteryMonitor.Services;

public enum TrayDisplayMode
{
    ChargePercent,
    Watt,
    CapacityMwh
}

public class TrayIconService : IDisposable
{
    private readonly Hardcodet.Wpf.TaskbarNotification.TaskbarIcon _trayIcon;
    private TrayDisplayMode _displayMode = TrayDisplayMode.ChargePercent;
    private BatteryInfo? _lastInfo;
    private bool _isDarkMode = true;

    public event Action? ShowRequested;
    public event Action? ExitRequested;

    public TrayDisplayMode DisplayMode
    {
        get => _displayMode;
        set
        {
            _displayMode = value;
            if (_lastInfo != null) UpdateIcon(_lastInfo);
            UpdateMenuChecks();
        }
    }

    public TrayIconService()
    {
        _trayIcon = new Hardcodet.Wpf.TaskbarNotification.TaskbarIcon
        {
            ToolTipText = "Battery Monitor",
            MenuActivation = Hardcodet.Wpf.TaskbarNotification.PopupActivationMode.RightClick
        };

        _trayIcon.TrayMouseDoubleClick += (_, _) => ShowRequested?.Invoke();

        BuildContextMenu();
        UpdateIcon(null);
    }

    private System.Windows.Controls.MenuItem _menuChargePercent = null!;
    private System.Windows.Controls.MenuItem _menuWatt = null!;
    private System.Windows.Controls.MenuItem _menuMwh = null!;

    private void BuildContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        _menuChargePercent = new System.Windows.Controls.MenuItem { Header = "Anzeige: Ladestand", IsChecked = true };
        _menuChargePercent.Click += (_, _) => DisplayMode = TrayDisplayMode.ChargePercent;

        _menuWatt = new System.Windows.Controls.MenuItem { Header = "Anzeige: Watt" };
        _menuWatt.Click += (_, _) => DisplayMode = TrayDisplayMode.Watt;

        _menuMwh = new System.Windows.Controls.MenuItem { Header = "Anzeige: mWh" };
        _menuMwh.Click += (_, _) => DisplayMode = TrayDisplayMode.CapacityMwh;

        menu.Items.Add(_menuChargePercent);
        menu.Items.Add(_menuWatt);
        menu.Items.Add(_menuMwh);
        menu.Items.Add(new System.Windows.Controls.Separator());

        var openItem = new System.Windows.Controls.MenuItem { Header = "Öffnen" };
        openItem.Click += (_, _) => ShowRequested?.Invoke();
        menu.Items.Add(openItem);

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Beenden" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(exitItem);

        _trayIcon.ContextMenu = menu;
    }

    private void UpdateMenuChecks()
    {
        _menuChargePercent.IsChecked = _displayMode == TrayDisplayMode.ChargePercent;
        _menuWatt.IsChecked = _displayMode == TrayDisplayMode.Watt;
        _menuMwh.IsChecked = _displayMode == TrayDisplayMode.CapacityMwh;
    }

    public void SetDarkMode(bool dark)
    {
        _isDarkMode = dark;
        if (_lastInfo != null) UpdateIcon(_lastInfo);
    }

    public void UpdateIcon(BatteryInfo? info)
    {
        _lastInfo = info;

        string text;
        if (_displayMode == TrayDisplayMode.Watt)
        {
            // Show the active watt value: charge rate when charging, discharge rate otherwise
            if (info == null)
                text = "?W";
            else if (info.Charging && info.ChargeRateWatt is > 0)
                text = $"{info.ChargeRateWatt:F0}";
            else if (!info.Charging && info.DischargeRateWatt > 0)
                text = $"{info.DischargeRateWatt:F0}";
            else
                text = "0";
        }
        else
        {
            text = _displayMode switch
            {
                TrayDisplayMode.ChargePercent => info != null ? $"{info.ChargePercent}" : "?",
                TrayDisplayMode.CapacityMwh => info != null ? $"{info.RemainingCapacityMwh / 1000}k" : "?",
                _ => "?"
            };
        }

        var tooltip = info != null
            ? $"Battery Monitor — {info.ChargePercent}%  ·  " +
              (info.Charging
                  ? $"Laden {info.ChargeRateWatt?.ToString("F1") ?? "…"}W"
                  : $"Entladung {info.DischargeRateWatt:F1}W") +
              $"  ·  {(info.Charging ? "Lädt" : info.PowerOnline ? "Am Strom" : "Akku")}"
            : "Battery Monitor";

        _trayIcon.ToolTipText = tooltip;
        _trayIcon.Icon = RenderTextIcon(text);
    }

    private System.Drawing.Icon RenderTextIcon(string text)
    {
        const int size = 64; // double size for crispness
        using var bmp = new Bitmap(size, size);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.Clear(System.Drawing.Color.Transparent);

        var fontSize = text.Length <= 2 ? 38f : text.Length <= 3 ? 30f : 24f;
        using var font = new Font("Segoe UI", fontSize, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
        var textColor = _isDarkMode ? System.Drawing.Color.White : System.Drawing.Color.FromArgb(30, 30, 50);
        using var brush = new SolidBrush(textColor);

        var measured = g.MeasureString(text, font);
        var x = (size - measured.Width) / 2f;
        var y = (size - measured.Height) / 2f;
        g.DrawString(text, font, brush, x, y);

        var hIcon = bmp.GetHicon();
        return System.Drawing.Icon.FromHandle(hIcon);
    }

    public void SetVisible(bool visible)
    {
        _trayIcon.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Dispose()
    {
        _trayIcon.Dispose();
    }
}
