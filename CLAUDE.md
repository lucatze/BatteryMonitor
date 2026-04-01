# Battery Monitor — CLAUDE.md

Echtzeit-Akku-Dashboard für Windows als portable Single-File-EXE.
Technologie: **C# 12 · .NET 8 · WPF** · kein MVVM-Framework · kein Designer-State

---

## Projektstruktur

```
Lenovo-Battery/
├── CLAUDE.md                          ← diese Datei
└── BatteryMonitor/
    ├── BatteryMonitor.csproj
    ├── GlobalUsings.cs                ← WPF/WinForms-Namenskonflikt-Auflösung
    ├── App.xaml / App.xaml.cs
    ├── MainWindow.xaml                ← UI-Layout (kein Binding, Code-Behind)
    ├── MainWindow.xaml.cs             ← Theme + UpdateUI-Logik
    ├── Controls/
    │   ├── GaugeControl.xaml          ← Kreisbogen-UserControl (120×120 Canvas)
    │   └── GaugeControl.xaml.cs       ← ArcSegment-Rendering, UpdateTheme()
    ├── Services/
    │   ├── BatteryService.cs          ← WMI-Polling, BatteryInfo record
    │   └── TrayIconService.cs         ← Tray-Icon, Kontext-Menü, Icon-Rendering
    └── Converters/
        └── ColorConverters.cs         ← XAML-Converter (aktuell kaum genutzt)
```

---

## Build & Publish

```bash
# Im Verzeichnis BatteryMonitor/ ausführen:

# Debug (schnell, für Entwicklung):
dotnet run

# Release-Build (prüft ob alles kompiliert):
dotnet build -c Release

# Portable Single-File-EXE → BatteryMonitor/dist/BatteryMonitor.exe
dotnet publish BatteryMonitor.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist
```

**Wichtig:** Publish-Ausgabe immer nach `BatteryMonitor/dist/` (nicht `../dist`).
Die Root-`dist/`-Variante erzeugt separate DLLs neben der EXE — das ist falsch.

**Voraussetzung:** .NET 8 SDK — `dotnet --version` muss `8.x.x` zeigen.
dotnet.exe liegt auf diesem Rechner unter `C:\Program Files\dotnet\dotnet.exe`.

---

## Abhängigkeiten (NuGet)

| Paket | Version | Zweck |
|---|---|---|
| `System.Management` | 10.0.5 | WMI-Abfragen |
| `Hardcodet.NotifyIcon.Wpf` | 2.0.1 | System-Tray-Icon |

`UseWPF=true` + `UseWindowsForms=true` — beide aktiviert, da `System.Drawing` (WinForms) für das Tray-Icon-Bitmap-Rendering gebraucht wird.

---

## Kritische Design-Entscheidungen

### GlobalUsings.cs — Namespace-Konflikte
WPF + WinForms teilen sich Typnamen (`Application`, `Color`, `Point` etc.).
Alle konflikthaften WPF-Typen werden in `GlobalUsings.cs` als globale Aliase definiert,
damit `System.Drawing.*` (WinForms) in `TrayIconService.cs` unqualifiziert nutzbar ist:

```csharp
global using Application = System.Windows.Application;
global using Color       = System.Windows.Media.Color;
global using Point       = System.Windows.Point;
global using Size        = System.Windows.Size;
global using Brush       = System.Windows.Media.Brush;
global using Brushes     = System.Windows.Media.Brushes;
global using UserControl = System.Windows.Controls.UserControl;
```

### Kein app.manifest
Die Datei `app.manifest` und `<ApplicationManifest>` im csproj wurden entfernt —
sie verursachten einen Crash beim Start der publizierten EXE durch DPI-Manifest-Konflikt
zwischen WinForms und einem benutzerdefinierten Manifest.

### Theme: Code-Behind statt XAML-Binding
Theme (Dark/Light) wird imperativ via `ApplyTheme()` in `MainWindow.xaml.cs` angewendet.
Keine `INotifyPropertyChanged`-Bindings, keine `DynamicResource`-Einträge.
`SolidColorBrush`-Instanzen sind als `static readonly` Felder deklariert und mit `.Freeze()` eingefroren.

### WMI-Datenquellen

| WMI-Klasse | Namespace | Abgerufene Felder |
|---|---|---|
| `BatteryStaticData` | `root\wmi` | DeviceName, ManufactureName, SerialNumber, DesignedCapacity |
| `BatteryFullChargedCapacity` | `root\wmi` | FullChargedCapacity |
| `BatteryStatus` | `root\wmi` | PowerOnline, Charging, Discharging, ChargeRate, DischargeRate, RemainingCapacity, Voltage |
| `BatteryRuntime` | `root\wmi` | EstimatedRuntime (Sekunden!) |

- Statische Daten werden einmalig beim Start geladen
- Live-Daten werden alle **3 Sekunden** via `DispatcherTimer` gepollt
- `BatteryRuntime.EstimatedRuntime` ist in **Sekunden** — durch 60 dividieren für Minuten
- `ChargeRate` / `DischargeRate` sind in **mW** — durch 1000 für Watt
- `Voltage` ist in **mV** — durch 1000 für Volt
- Wenn `Charging=true && ChargeRate=0`: `ChargeRateWatt = null` → UI zeigt `…` (WMI-Verzögerung)

---

## BatteryInfo Record

```csharp
record BatteryInfo {
    // Live (aus BatteryStatus)
    bool PowerOnline;
    bool Charging;
    double? ChargeRateWatt;      // null wenn Charging && WMI noch 0 liefert
    double DischargeRateWatt;
    int RemainingCapacityMwh;
    double VoltageMv;
    int? EstimatedRuntimeMinutes; // aus BatteryRuntime (Sekunden/60)

    // Statisch (aus BatteryStaticData + BatteryFullChargedCapacity)
    string DeviceName, Manufacturer, SerialNumber;
    int DesignedCapacityMwh, FullChargedCapacityMwh;

    // Computed
    int ChargePercent      // RemainingCap / FullCap * 100
    double HealthPercent   // FullCap / DesignedCap * 100
    int? EstimatedTimeToFullMinutes  // aus ChargeRate + verbleibender Kapazität
}
```

---

## UI-Spezifikation (aktueller Stand)

### Fenster
- Größe: `Width="580" Height="700"`, `ResizeMode="NoResize"`, `WindowStyle="None"`
- Schließen → minimiert in Tray (nicht beendet)
- Titelleiste: 40px, ziehbar via `DragMove()`

### Gauge-Reihe
- Canvas: 120×120px, Kreisbogen: Start 230°, Sweep 280°, Radius 52, Dicke 9px, runde Caps
- Farb-Schwellen:
  - Ladestand: ≥50% Grün / ≥20% Amber / <20% Rot
  - Gesundheit: ≥85% Grün / ≥65% Amber / <65% Rot
- Watt-Box (3. Spalte): gleiche Schriftgrößen für Ladeleistung und Entladung (12pt Label, 22pt Wert)
- `LADELEISTUNG`-Label ist grün eingefärbt (gleiche Farbe wie der Wert)
- Einheit: `W` direkt an die Zahl angehängt (z.B. `45.2 W`), kein separates "Watt"-TextBlock

### Farbpalette

| Farbe | Dark Hex | Light Hex | Verwendung |
|---|---|---|---|
| Bg | `#0D0F14` | `#F3F4F6` | Fensterhintergrund |
| CardBg | `#141720` | `#FFFFFF` | Karten-Hintergrund |
| CardBorder | `#1E2330` | `#D8DBE6` | Karten-Rahmen, Trenner |
| TextPrimary | `#FFFFFF` | `#0A0A0A` | Haupttext, Werte |
| TextMuted | `#B0BCDA` | `#4A5568` | Labels, Einheiten |
| TextDim | `#606E90` | `#909AB0` | Subzeilen, Timestamps |
| Green | `#30D158` | gleich | Laden, hoher Ladestand/Gesundheit |
| Amber | `#FF9F0A` | gleich | Am Strom (kein Laden), mittlere Werte |
| Red | `#FF453A` | gleich | Akku (kein Strom), niedrige Werte |
| AccentBlue | `#4A9EFF` | `#1D4ED8` | Karten-Titel, Gauge-Label |

### Taskbar-Overlay-Badge
- `TaskbarItemInfo.Overlay` in `MainWindow.xaml` / gerendert in `MainWindow.xaml.cs`
- 32×32px `RenderTargetBitmap` via `DrawingVisual` (kein GDI+)
- Zeigt Ladestand als Prozentzahl auf farbigem Kreis:
  - Blau (`#4A9EFF`) — lädt
  - Grün / Amber / Rot — gleiche Schwellen wie Gauge
- Sichtbar wenn das Fenster in der Taskleiste erscheint (offen oder angepinnt)

### Tray-Icon
- `TrayDisplayMode`: `ChargePercent` | `Watt` | `CapacityMwh`
- Watt-Modus: zeigt Ladewatt wenn lädt, Entladewatt wenn entlädt
- Icon-Bitmap: 64×64px, Segoe UI Bold, Schriftgröße 38/30/24pt (nach Textlänge ≤2/≤3/>3)
- Dark Mode: weiße Schrift; Light Mode: dunkle Schrift (`#1E1E32`)
- Tooltip: `Battery Monitor — 87% · 20.4W · Lädt`
- Rechtsklick-Menü: Radio-Auswahl Anzeige-Modus + Öffnen + Beenden
- Doppelklick → Fenster öffnen

---

## Git-Workflow

```bash
# Nur Source committen — bin/, obj/, dist/ sind via .gitignore ausgeschlossen
git add BatteryMonitor/<datei>
git commit -m "..."

# Nach Änderungen: erst build prüfen, dann publish, dann committen
dotnet build -c Release          # kompiliert?
# → EXE testen
dotnet publish ... -o dist       # EXE aktualisieren
git add ...
git commit
```

`.gitignore` liegt in `BatteryMonitor/` und schließt aus: `bin/`, `obj/`, `dist/`, `.vs/`, `*.user`

---

## Häufige Probleme & Lösungen

| Problem | Ursache | Lösung |
|---|---|---|
| Ambiguous reference `Application`, `Color`, etc. | WPF + WinForms beide aktiviert | `GlobalUsings.cs` mit expliziten WPF-Aliasen |
| EXE crashed nach Publish mit DPI-Fehler | `app.manifest` konfligiert mit WinForms | `app.manifest` und `<ApplicationManifest>` aus csproj entfernen |
| `BatteryRuntime.EstimatedRuntime` zu groß | Wert ist in Sekunden, nicht Minuten | `secs / 60` + Sanity-Check `secs < 999999` |
| Tray-Icon-Text unscharf | Bitmap zu klein (16px) | Auf 64px erhöht, Font proportional skaliert |
| Gauge-Spalten zu schmal | Fixe `150px`-Spalten | Auf `*` (gleichmäßig) geändert |
