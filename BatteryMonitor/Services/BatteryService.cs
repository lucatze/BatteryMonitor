using System.Management;
using System.Windows.Threading;

namespace BatteryMonitor.Services;

public record BatteryInfo
{
    // Live
    public bool PowerOnline { get; init; }
    public bool Charging { get; init; }
    public double? ChargeRateWatt { get; init; }
    public double DischargeRateWatt { get; init; }
    public int RemainingCapacityMwh { get; init; }
    public double VoltageMv { get; init; }

    // Estimates
    public int? EstimatedRuntimeMinutes { get; init; }

    // Static
    public string DeviceName { get; init; } = "";
    public string Manufacturer { get; init; } = "";
    public string SerialNumber { get; init; } = "";
    public int DesignedCapacityMwh { get; init; }
    public int FullChargedCapacityMwh { get; init; }

    // Computed
    public int ChargePercent => FullChargedCapacityMwh > 0
        ? (int)Math.Round(RemainingCapacityMwh / (double)FullChargedCapacityMwh * 100)
        : 0;

    public double HealthPercent => DesignedCapacityMwh > 0
        ? Math.Round(FullChargedCapacityMwh / (double)DesignedCapacityMwh * 100, 1)
        : 0;

    // Estimated time to full (minutes), computed from charge rate
    public int? EstimatedTimeToFullMinutes
    {
        get
        {
            if (!Charging || ChargeRateWatt is null or <= 0) return null;
            var remainingMwh = FullChargedCapacityMwh - RemainingCapacityMwh;
            if (remainingMwh <= 0) return 0;
            // ChargeRate is in mW from WMI, ChargeRateWatt is W → mW = W*1000
            var minutes = remainingMwh / (ChargeRateWatt.Value * 1000.0) * 60.0;
            return (int)Math.Round(minutes);
        }
    }
}

public class BatteryService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private string _deviceName = "";
    private string _manufacturer = "";
    private string _serialNumber = "";
    private int _designedCapacity;
    private int _fullChargedCapacity;
    private string? _errorMessage;

    public event Action<BatteryInfo>? BatteryUpdated;
    public event Action<string?>? ErrorChanged;

    public BatteryService()
    {
        LoadStaticData();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _timer.Tick += (_, _) => Poll();
        _timer.Start();

        Poll();
    }

    private void LoadStaticData()
    {
        try
        {
            using var staticSearcher = new ManagementObjectSearcher("root\\wmi",
                "SELECT DeviceName, ManufactureName, SerialNumber, DesignedCapacity FROM BatteryStaticData");
            foreach (var obj in staticSearcher.Get())
            {
                _deviceName = obj["DeviceName"]?.ToString() ?? "";
                _manufacturer = obj["ManufactureName"]?.ToString() ?? "";
                _serialNumber = obj["SerialNumber"]?.ToString()?.Trim() ?? "";
                _designedCapacity = Convert.ToInt32(obj["DesignedCapacity"]);
                break;
            }

            using var fullCapSearcher = new ManagementObjectSearcher("root\\wmi",
                "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity");
            foreach (var obj in fullCapSearcher.Get())
            {
                _fullChargedCapacity = Convert.ToInt32(obj["FullChargedCapacity"]);
                break;
            }

            SetError(null);
        }
        catch (Exception ex)
        {
            SetError($"WMI-Fehler (statisch): {ex.Message}");
        }
    }

    private void Poll()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\wmi",
                "SELECT PowerOnline, Charging, Discharging, ChargeRate, DischargeRate, RemainingCapacity, Voltage FROM BatteryStatus");

            // Get estimated runtime from BatteryRuntime
            int? estimatedRuntimeMin = null;
            try
            {
                using var rtSearcher = new ManagementObjectSearcher("root\\wmi",
                    "SELECT EstimatedRuntime FROM BatteryRuntime");
                foreach (var rtObj in rtSearcher.Get())
                {
                    var secs = Convert.ToInt32(rtObj["EstimatedRuntime"]);
                    if (secs > 0 && secs < 999999)
                        estimatedRuntimeMin = secs / 60;
                    break;
                }
            }
            catch { /* BatteryRuntime not available on all systems */ }

            foreach (var obj in searcher.Get())
            {
                var charging = Convert.ToBoolean(obj["Charging"]);
                var chargeRate = Convert.ToInt32(obj["ChargeRate"]);

                double? chargeRateWatt = null;
                if (charging && chargeRate > 0)
                    chargeRateWatt = Math.Round(chargeRate / 1000.0, 1);
                else if (!charging)
                    chargeRateWatt = 0;
                // charging && chargeRate == 0 → null (UI shows "…")

                var info = new BatteryInfo
                {
                    PowerOnline = Convert.ToBoolean(obj["PowerOnline"]),
                    Charging = charging,
                    ChargeRateWatt = chargeRateWatt,
                    DischargeRateWatt = Math.Round(Convert.ToInt32(obj["DischargeRate"]) / 1000.0, 1),
                    RemainingCapacityMwh = Convert.ToInt32(obj["RemainingCapacity"]),
                    VoltageMv = Convert.ToDouble(obj["Voltage"] ?? 0),
                    EstimatedRuntimeMinutes = estimatedRuntimeMin,
                    DeviceName = _deviceName,
                    Manufacturer = _manufacturer,
                    SerialNumber = _serialNumber,
                    DesignedCapacityMwh = _designedCapacity,
                    FullChargedCapacityMwh = _fullChargedCapacity
                };

                SetError(null);
                BatteryUpdated?.Invoke(info);
                break;
            }
        }
        catch (Exception ex)
        {
            SetError($"WMI-Fehler: {ex.Message}");
        }
    }

    private void SetError(string? msg)
    {
        if (_errorMessage != msg)
        {
            _errorMessage = msg;
            ErrorChanged?.Invoke(msg);
        }
    }

    public void Dispose()
    {
        _timer.Stop();
    }
}
