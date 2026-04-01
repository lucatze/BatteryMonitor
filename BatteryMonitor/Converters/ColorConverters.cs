using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace BatteryMonitor.Converters;

public class ChargeToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x30, 0xD1, 0x58));
    private static readonly SolidColorBrush Amber = new(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly SolidColorBrush Red = new(Color.FromRgb(0xFF, 0x45, 0x3A));

    static ChargeToColorConverter()
    {
        Green.Freeze(); Amber.Freeze(); Red.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var pct = System.Convert.ToDouble(value);
        return pct >= 50 ? Green : pct >= 20 ? Amber : Red;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class HealthToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x30, 0xD1, 0x58));
    private static readonly SolidColorBrush Amber = new(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly SolidColorBrush Red = new(Color.FromRgb(0xFF, 0x45, 0x3A));

    static HealthToColorConverter()
    {
        Green.Freeze(); Amber.Freeze(); Red.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var pct = System.Convert.ToDouble(value);
        return pct >= 85 ? Green : pct >= 65 ? Amber : Red;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToColorConverter : IValueConverter
{
    private static readonly SolidColorBrush Green = new(Color.FromRgb(0x30, 0xD1, 0x58));
    private static readonly SolidColorBrush Amber = new(Color.FromRgb(0xFF, 0x9F, 0x0A));
    private static readonly SolidColorBrush Red = new(Color.FromRgb(0xFF, 0x45, 0x3A));

    static StatusToColorConverter()
    {
        Green.Freeze(); Amber.Freeze(); Red.Freeze();
    }

    // parameter: "Charging|PowerOnline" as two bools packed into a string "True,True"
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string status)
        {
            return status switch
            {
                "Charging" => Green,
                "PluggedIn" => Amber,
                _ => Red
            };
        }
        return Red;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NullableWattConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double d)
            return d.ToString("F1");
        return "\u2026"; // "…"
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class MwhToWhConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var mwh = System.Convert.ToInt32(value);
        return $"{mwh:N0} mWh  ({mwh / 1000.0:F1} Wh)";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class PercentToAngleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var pct = Math.Max(0, Math.Min(100, System.Convert.ToDouble(value)));
        return pct / 100.0 * 280.0; // 280° total arc span
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
