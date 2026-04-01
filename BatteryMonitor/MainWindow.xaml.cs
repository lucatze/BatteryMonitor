using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using BatteryMonitor.Services;

namespace BatteryMonitor;

public partial class MainWindow : Window
{
    private readonly BatteryService _batteryService;
    private readonly TrayIconService _trayService;
    private bool _isDarkMode = true;
    private BadgeDisplayMode _badgeMode = BadgeDisplayMode.ChargePercent;
    private BatteryInfo? _lastBatteryInfo;

    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x30, 0xD1, 0x58));
    private static readonly SolidColorBrush Amber = new(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly SolidColorBrush Red = new(Color.FromRgb(0xFF, 0x45, 0x3A));
    private static readonly SolidColorBrush AccentBlue = new(Color.FromRgb(0x4A, 0x9E, 0xFF));

    // Dark theme — bright whites
    private static readonly SolidColorBrush DarkBg = new(Color.FromRgb(0x0D, 0x0F, 0x14));
    private static readonly SolidColorBrush DarkCardBg = new(Color.FromRgb(0x14, 0x17, 0x20));
    private static readonly SolidColorBrush DarkCardBorder = new(Color.FromRgb(0x1E, 0x23, 0x30));
    private static readonly SolidColorBrush DarkTextPrimary = new(Color.FromRgb(0xFF, 0xFF, 0xFF)); // pure white
    private static readonly SolidColorBrush DarkTextMuted = new(Color.FromRgb(0xB0, 0xBC, 0xDA));  // much brighter muted
    private static readonly SolidColorBrush DarkTextDim = new(Color.FromRgb(0x60, 0x6E, 0x90));    // brighter dim

    // Light theme — pure black text
    private static readonly SolidColorBrush LightBg = new(Color.FromRgb(0xF3, 0xF4, 0xF6));
    private static readonly SolidColorBrush LightCardBg = new(Color.FromRgb(0xFF, 0xFF, 0xFF));
    private static readonly SolidColorBrush LightCardBorder = new(Color.FromRgb(0xD8, 0xDB, 0xE6));
    private static readonly SolidColorBrush LightTextPrimary = new(Color.FromRgb(0x0A, 0x0A, 0x0A)); // near-black
    private static readonly SolidColorBrush LightTextMuted = new(Color.FromRgb(0x4A, 0x55, 0x68));   // dark grey
    private static readonly SolidColorBrush LightTextDim = new(Color.FromRgb(0x90, 0x9A, 0xB0));
    private static readonly SolidColorBrush LightAccentBlue = new(Color.FromRgb(0x1D, 0x4E, 0xD8));

    static MainWindow()
    {
        Green.Freeze(); Amber.Freeze(); Red.Freeze(); AccentBlue.Freeze();
        DarkBg.Freeze(); DarkCardBg.Freeze(); DarkCardBorder.Freeze();
        DarkTextPrimary.Freeze(); DarkTextMuted.Freeze(); DarkTextDim.Freeze();
        LightBg.Freeze(); LightCardBg.Freeze(); LightCardBorder.Freeze();
        LightTextPrimary.Freeze(); LightTextMuted.Freeze(); LightTextDim.Freeze();
        LightAccentBlue.Freeze();
    }

    private readonly AppSettings _settings;

    public MainWindow()
    {
        InitializeComponent();

        // Load settings and apply language before building UI
        _settings = AppSettings.Load();
        var lang = string.IsNullOrEmpty(_settings.Language)
            ? Loc.DetectSystemLanguage()
            : _settings.Language;
        Loc.SetLanguage(lang);

        _trayService = new TrayIconService();
        _trayService.ShowRequested += () => Dispatcher.Invoke(ShowFromTray);
        _trayService.ExitRequested += () => Dispatcher.Invoke(ExitApp);
        _trayService.BadgeDisplayModeChanged += mode => Dispatcher.Invoke(() =>
        {
            _badgeMode = mode;
            if (_lastBatteryInfo != null) UpdateBadge(_lastBatteryInfo);
        });

        _batteryService = new BatteryService();
        _batteryService.BatteryUpdated += info => Dispatcher.Invoke(() => UpdateUI(info));
        _batteryService.ErrorChanged += err => Dispatcher.Invoke(() => ErrorText.Text = err ?? "");

        Closing += (_, e) =>
        {
            e.Cancel = true;
            HideToTray();
        };

        ApplyTheme();
        ApplyLocalization();
    }

    private SolidColorBrush Bg => _isDarkMode ? DarkBg : LightBg;
    private SolidColorBrush CardBg => _isDarkMode ? DarkCardBg : LightCardBg;
    private SolidColorBrush CardBorder => _isDarkMode ? DarkCardBorder : LightCardBorder;
    private SolidColorBrush TextPrimary => _isDarkMode ? DarkTextPrimary : LightTextPrimary;
    private SolidColorBrush TextMuted => _isDarkMode ? DarkTextMuted : LightTextMuted;
    private SolidColorBrush TextDim => _isDarkMode ? DarkTextDim : LightTextDim;
    private SolidColorBrush Accent => _isDarkMode ? AccentBlue : LightAccentBlue;

    private void ApplyTheme()
    {
        RootWindow.Background = Bg;
        RootBorder.BorderBrush = CardBorder;
        TitleBarGrid.Background = Bg;
        RootGrid.Background = Bg;

        // Title bar
        TitleIcon.Foreground = Amber;
        TitleText.Foreground = TextPrimary;
        StatusText.Foreground = TextMuted;
        ThemeToggleBtn.Content = _isDarkMode ? "\u2600" : "\u263E"; // sun / moon
        ThemeToggleBtn.Foreground = TextMuted;
        MinimizeBtn.Foreground = TextMuted;
        CloseBtn.Foreground = TextPrimary;

        // Device subline
        DeviceSubline.Foreground = TextDim;

        // Watt box
        WattBoxBorder.Background = CardBg;
        WattBoxBorder.BorderBrush = CardBorder;
        WattBoxLabel.Foreground = Green;
        ChargeRateText.Foreground = Green;
        WattSep.Fill = CardBorder;
        DischargeLabel.Foreground = Amber;
        DischargeRateText.Foreground = Amber;

        // Estimate card
        EstimateCard.Background = CardBg;
        EstimateCard.BorderBrush = CardBorder;
        EstimateTitle.Foreground = Accent;
        EstimateSep.Fill = CardBorder;
        EstimateLabel.Foreground = TextDim;
        EstimateTimeText.Foreground = TextPrimary;
        EstimateUnitText.Foreground = TextMuted;
        VoltageLabel.Foreground = TextDim;
        VoltageText.Foreground = TextPrimary;
        VoltUnitText.Foreground = TextMuted;
        PowerLabel.Foreground = TextDim;
        PowerText.Foreground = TextPrimary;
        PowerUnitText.Foreground = TextMuted;

        // Capacity card
        CapacityCard.Background = CardBg;
        CapacityCard.BorderBrush = CardBorder;
        CapTitle.Foreground = Accent;
        CapSep.Fill = CardBorder;
        DesignCapLabel.Foreground = TextMuted;
        DesignCapText.Foreground = TextPrimary;
        FullCapLabel.Foreground = TextMuted;
        FullCapText.Foreground = TextPrimary;
        RemCapLabel.Foreground = TextMuted;
        BarBg.Fill = CardBorder;

        // Device card
        DeviceCard.Background = CardBg;
        DeviceCard.BorderBrush = CardBorder;
        DeviceTitle.Foreground = Accent;
        DeviceSep.Fill = CardBorder;
        ModelLabel.Foreground = TextMuted;
        ModelText.Foreground = TextPrimary;
        MfgLabel.Foreground = TextMuted;
        ManufacturerText.Foreground = TextPrimary;
        SnLabel.Foreground = TextMuted;
        SerialText.Foreground = TextPrimary;

        // Footer
        ErrorText.Foreground = Red;
        UpdatedText.Foreground = TextDim;

        // Gauge colors update
        ChargeGauge.UpdateTheme(TextPrimary, TextMuted, Accent, CardBorder);
        HealthGauge.UpdateTheme(TextPrimary, TextMuted, Accent, CardBorder);
    }

    private void ApplyLocalization()
    {
        LangBtn.Content = Loc.Current.ToUpperInvariant();
        LangBtn.Foreground = TextMuted;

        ChargeGauge.Label  = Loc.Get("charge_level");
        HealthGauge.Label  = Loc.Get("health");

        WattBoxLabel.Text  = Loc.Get("charge_rate");
        DischargeLabel.Text = Loc.Get("discharge");

        EstimateTitle.Text = Loc.Get("forecast");
        VoltageLabel.Text  = Loc.Get("voltage");
        VoltUnitText.Text  = Loc.Get("volt");
        PowerUnitText.Text = Loc.Get("watt");

        CapTitle.Text      = Loc.Get("capacity");
        DesignCapLabel.Text = Loc.Get("design_cap");
        FullCapLabel.Text  = Loc.Get("full_cap");
        RemCapLabel.Text   = Loc.Get("remaining");

        DeviceTitle.Text   = Loc.Get("device");
        ModelLabel.Text    = Loc.Get("model");
        MfgLabel.Text      = Loc.Get("manufacturer");
        SnLabel.Text       = Loc.Get("serial");

        _trayService.ApplyLanguage();

        if (_lastBatteryInfo != null) UpdateUI(_lastBatteryInfo);
    }

    private void LangBtn_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu { PlacementTarget = LangBtn };
        foreach (var (code, name) in Loc.Languages)
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = name,
                IsChecked = code == Loc.Current,
            };
            var captured = code;
            item.Click += (_, _) =>
            {
                Loc.SetLanguage(captured);
                _settings.Language = captured;
                _settings.Save();
                ApplyLocalization();
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void ToggleTheme_Click(object sender, RoutedEventArgs e)
    {
        _isDarkMode = !_isDarkMode;
        ApplyTheme();
        _trayService.SetDarkMode(_isDarkMode);
    }

    private void UpdateUI(BatteryInfo info)
    {
        // Status dot + text
        if (info.Charging)
        {
            StatusDot.Fill = Green;
            StatusText.Text = Loc.Get("charging");
        }
        else if (info.PowerOnline)
        {
            StatusDot.Fill = Amber;
            StatusText.Text = Loc.Get("on_power");
        }
        else
        {
            StatusDot.Fill = Red;
            StatusText.Text = Loc.Get("on_battery");
        }

        // Device subline
        DeviceSubline.Text = $"{info.DeviceName}  \u00B7  {info.Manufacturer}  \u00B7  SN {info.SerialNumber}";

        // Gauges
        var chargeColor = info.ChargePercent >= 50 ? Green : info.ChargePercent >= 20 ? Amber : Red;
        ChargeGauge.Value = info.ChargePercent;
        ChargeGauge.GaugeColor = chargeColor;

        var healthColor = info.HealthPercent >= 85 ? Green : info.HealthPercent >= 65 ? Amber : Red;
        HealthGauge.Value = info.HealthPercent;
        HealthGauge.GaugeColor = healthColor;

        // Watt box
        ChargeRateText.Text = info.ChargeRateWatt.HasValue ? $"{info.ChargeRateWatt.Value:F1} W" : "\u2026";
        DischargeRateText.Text = $"{info.DischargeRateWatt:F1} W";

        // Estimates card
        if (info.Charging)
        {
            EstimateLabel.Text = Loc.Get("full_in");
            var ttf = info.EstimatedTimeToFullMinutes;
            if (ttf.HasValue && ttf.Value > 0)
            {
                var h = ttf.Value / 60;
                var m = ttf.Value % 60;
                EstimateTimeText.Text = h > 0 ? $"{h}:{m:D2}" : $"{m}";
                EstimateUnitText.Text = h > 0 ? Loc.Get("hours_min") : Loc.Get("minutes");
            }
            else
            {
                EstimateTimeText.Text = "\u2026";
                EstimateUnitText.Text = "";
            }
            PowerLabel.Text = Loc.Get("charge_rate");
            PowerText.Text = info.ChargeRateWatt?.ToString("F1") ?? "\u2026";
        }
        else
        {
            EstimateLabel.Text = Loc.Get("runtime");
            var rt = info.EstimatedRuntimeMinutes;
            if (rt.HasValue && rt.Value > 0)
            {
                var h = rt.Value / 60;
                var m = rt.Value % 60;
                EstimateTimeText.Text = h > 0 ? $"{h}:{m:D2}" : $"{m}";
                EstimateUnitText.Text = h > 0 ? Loc.Get("hours_min") : Loc.Get("minutes");
            }
            else
            {
                EstimateTimeText.Text = "\u2026";
                EstimateUnitText.Text = "";
            }
            PowerLabel.Text = Loc.Get("consumption");
            PowerText.Text = info.DischargeRateWatt > 0 ? info.DischargeRateWatt.ToString("F1") : "\u2026";
        }

        // Voltage
        VoltageText.Text = info.VoltageMv > 0 ? (info.VoltageMv / 1000.0).ToString("F2") : "\u2026";

        // Capacity card
        DesignCapText.Text = FormatMwh(info.DesignedCapacityMwh);
        FullCapText.Text = FormatMwh(info.FullChargedCapacityMwh);
        RemainingCapText.Text = FormatMwh(info.RemainingCapacityMwh);
        RemainingCapText.Foreground = chargeColor;

        // Progress bar
        var barPct = info.FullChargedCapacityMwh > 0
            ? Math.Clamp(info.RemainingCapacityMwh / (double)info.FullChargedCapacityMwh, 0, 1)
            : 0;
        ChargeBar.Width = ChargeBar.Parent is Grid grid
            ? grid.ActualWidth * barPct
            : 0;
        ChargeBar.Fill = chargeColor;

        // Device card
        ModelText.Text = info.DeviceName;
        ManufacturerText.Text = info.Manufacturer;
        SerialText.Text = info.SerialNumber;

        // Footer
        UpdatedText.Text = $"{Loc.Get("updated")} {DateTime.Now:HH:mm:ss}";

        // Taskbar overlay badge
        _lastBatteryInfo = info;
        UpdateBadge(info);

        // Tray
        _trayService.UpdateIcon(info);
    }

    private void UpdateBadge(BatteryInfo info)
    {
        if (_badgeMode == BadgeDisplayMode.Off)
        {
            TaskbarInfo.Overlay = null;
            return;
        }

        string text;
        if (_badgeMode == BadgeDisplayMode.Watt)
        {
            var w = info.Charging ? info.ChargeRateWatt : info.DischargeRateWatt;
            text = w.HasValue && w.Value > 0 ? $"{w.Value:F0}W" : "…";
        }
        else
        {
            text = $"{info.ChargePercent}";
        }

        var bg = info.Charging
            ? Color.FromRgb(0x4A, 0x9E, 0xFF)
            : info.ChargePercent >= 50
                ? Color.FromRgb(0x30, 0xD1, 0x58)
                : info.ChargePercent >= 20
                    ? Color.FromRgb(0xFF, 0x9F, 0x0A)
                    : Color.FromRgb(0xFF, 0x45, 0x3A);

        TaskbarInfo.Overlay = RenderBadge(text, bg, Colors.White);
    }

    private static string FormatMwh(int mwh) => $"{mwh:N0} mWh  ({mwh / 1000.0:F1} Wh)";

    private static ImageSource RenderBadge(string text, Color bg, Color fg)
    {
        const int size = 32;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawEllipse(new SolidColorBrush(bg), null, new Point(size / 2.0, size / 2.0), size / 2.0, size / 2.0);
            var ft = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                System.Windows.FlowDirection.LeftToRight,
                new Typeface(new System.Windows.Media.FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
                text.Length <= 2 ? 16 : 12,
                new SolidColorBrush(fg),
                96);
            dc.DrawText(ft, new Point((size - ft.Width) / 2, (size - ft.Height) / 2));
        }
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1)
            DragMove();
    }

    private void MinimizeToTray_Click(object sender, RoutedEventArgs e) => HideToTray();
    private void Close_Click(object sender, RoutedEventArgs e) => ExitApp();

    private void HideToTray()
    {
        Hide();
        _trayService.SetVisible(true);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _trayService.Dispose();
        _batteryService.Dispose();
        Application.Current.Shutdown();
    }
}
