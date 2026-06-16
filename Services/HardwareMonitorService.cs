using System.IO;
using System.Management;
using LibreHardwareMonitor.Hardware;

namespace Klyr.Services
{
    /// <summary>
    /// Snapshot des capteurs matériels à un instant T.
    /// Toutes les valeurs sont nullable : un capteur peut être indisponible
    /// (pas de droits admin, GPU non supporté, capteur absent).
    /// </summary>
    public sealed class HardwareSnapshot
    {
        public float? CpuTempC      { get; set; }
        public float? CpuLoadPct    { get; set; }
        public float? GpuTempC      { get; set; }
        public float? GpuLoadPct    { get; set; }
        public string GpuName       { get; set; } = "—";
        public float? GpuVramUsedMb { get; set; }
        public float? GpuVramTotalMb{ get; set; }
        public float? DiskReadKBs   { get; set; }
        public float? DiskWriteKBs  { get; set; }
        public float? DiskTempC     { get; set; }
        public float[] CoreLoads    { get; set; } = System.Array.Empty<float>();
    }

    /// <summary>
    /// v2.3.0 — Lecture des capteurs matériels via LibreHardwareMonitor.
    /// Inspiré de VoltAir mais étendu (toutes marques GPU, températures disque, par-cœur CPU).
    ///
    /// IMPORTANT :
    ///  - La lecture des températures nécessite les droits admin (accès ring0 au driver).
    ///    Sans admin, les loads/usages restent disponibles mais les temps sont null.
    ///  - Singleton thread-safe. Computer.Open() une seule fois, Update() à chaque poll.
    ///  - Ne JAMAIS throw vers l'appelant : un capteur absent renvoie null, c'est tout.
    /// </summary>
    public sealed class HardwareMonitorService : IDisposable
    {
        private static readonly Lazy<HardwareMonitorService> _lazy =
            new(() => new HardwareMonitorService());
        public static HardwareMonitorService Instance => _lazy.Value;

        private readonly object _lock = new();
        private Computer? _computer;
        private bool _initFailed;
        private bool _disposed;

        private HardwareMonitorService() { }

        /// <summary>
        /// Initialise le Computer LibreHardwareMonitor (idempotent).
        /// Best-effort : si l'init échoue (drivers, perms), on log et on désactive.
        /// </summary>
        private bool EnsureInitialized()
        {
            if (_initFailed) return false;
            if (_computer != null) return true;

            lock (_lock)
            {
                if (_computer != null) return true;
                if (_initFailed) return false;

                try
                {
                    var computer = new Computer
                    {
                        IsCpuEnabled         = true,
                        IsGpuEnabled         = true,
                        IsMemoryEnabled      = true,
                        IsStorageEnabled     = true,
                        // Activé : la carte mère (Super I/O) expose souvent une température "CPU"
                        // utile en secours quand le Tctl/Tdie du CPU lit 0 (fréquent sur Ryzen).
                        IsMotherboardEnabled = true,
                        IsControllerEnabled  = false,
                        IsNetworkEnabled     = false,
                        IsBatteryEnabled     = false
                    };
                    computer.Open();
                    _computer = computer;
                    LogService.Instance.Info(Klyr.Resources.Strings.Log_SensorsInitialized, "Système");
                    return true;
                }
                catch (Exception ex)
                {
                    _initFailed = true;
                    LogService.Instance.Warn(
                        $"Capteurs matériels indisponibles : {ex.Message}. " +
                        "Les températures nécessitent les droits administrateur.",
                        "Système");
                    return false;
                }
            }
        }

        /// <summary>
        /// Lit un snapshot complet des capteurs. Best-effort, ne throw jamais.
        /// Appelé depuis le timer du MainViewModel (toutes les 2 s).
        /// </summary>
        public HardwareSnapshot Read()
        {
            var snap = new HardwareSnapshot();
            if (_disposed || !EnsureInitialized() || _computer == null)
                return snap;

            try
            {
                lock (_lock)
                {
                    var coreLoads = new List<float>();
                    float? mbCpuTemp = null;   // température CPU vue par la carte mère (secours)

                    foreach (var hw in _computer.Hardware)
                    {
                        hw.Update();
                        foreach (var sub in hw.SubHardware) sub.Update();

                        switch (hw.HardwareType)
                        {
                            case HardwareType.Cpu:
                                ReadCpu(hw, snap, coreLoads);
                                break;
                            case HardwareType.GpuNvidia:
                            case HardwareType.GpuAmd:
                            case HardwareType.GpuIntel:
                                ReadGpu(hw, snap);
                                break;
                            case HardwareType.Storage:
                                ReadStorage(hw, snap);
                                break;
                            case HardwareType.Motherboard:
                                mbCpuTemp ??= ReadMotherboardCpuTemp(hw);
                                break;
                        }
                    }

                    // Secours 1 : carte mère (Super I/O) — nécessite aussi le ring0.
                    snap.CpuTempC ??= mbCpuTemp;
                    // Secours 2 : zone thermique ACPI via WMI — INDÉPENDANT du driver ring0,
                    // fonctionne même si un autre logiciel verrouille WinRing0.
                    if (snap.CpuTempC is null or <= 0f)
                        snap.CpuTempC = ReadWmiThermalZone();

                    snap.CoreLoads = coreLoads.ToArray();
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Lecture capteurs échouée : {ex.Message}", "Système");
            }

            return snap;
        }

        private static void ReadCpu(IHardware hw, HardwareSnapshot snap, List<float> coreLoads)
        {
            // FIX température : priorité robuste Intel + AMD, en IGNORANT les valeurs invalides.
            // Un capteur qui renvoie 0 (ou une valeur absurde) est ignoré — un CPU au repos est
            // à ~35-50°C, donc 0°C = lecture invalide, pas une vraie température.
            float? packageTemp = null;   // 1) "CPU Package" (Intel) / "Core (Tctl/Tdie)" (AMD)
            float? averageTemp = null;   // 2) "Core Average"
            float? maxTemp     = null;   // 3) "Core Max"
            float? perCoreMax  = null;   // 4) max des capteurs "Core #x" / "CPU Core #x"
            float? anyTemp     = null;   // 5) catch-all : tout capteur de température CPU valide

            foreach (var s in hw.Sensors)
            {
                if (s.SensorType == SensorType.Load)
                {
                    if (s.Value is float lv)
                    {
                        if (s.Name == "CPU Total")        snap.CpuLoadPct = lv;
                        else if (s.Name.StartsWith("CPU Core #")) coreLoads.Add(lv);
                    }
                    continue;
                }

                if (s.SensorType != SensorType.Temperature) continue;
                if (s.Value is not float v) continue;

                // Ignore les valeurs invalides (0 ou hors plage physique plausible)
                if (v <= 0f || v > 130f) continue;

                string n = s.Name;
                if (n.Contains("Package") || n.Contains("Tctl") || n.Contains("Tdie"))
                    packageTemp = packageTemp is null ? v : Math.Max(packageTemp.Value, v);
                else if (n.Contains("Average"))
                    averageTemp = v;
                else if (n.Contains("Max"))
                    maxTemp = v;
                else if (n.Contains("Core") || n.Contains("CPU"))
                    perCoreMax = perCoreMax is null ? v : Math.Max(perCoreMax.Value, v);

                anyTemp = anyTemp is null ? v : Math.Max(anyTemp.Value, v);
            }

            // Température CPU encore calculée pour le rapport diagnostic uniquement (affiche "n/a"
            // si indisponible). Non affichée dans l'UI ni utilisée dans le Performance Score.
            snap.CpuTempC = packageTemp ?? averageTemp ?? maxTemp ?? perCoreMax ?? anyTemp;
        }

        // Cache du fallback WMI (la requête ACPI est throttlée à 1 / 5 s)
        private static float? _wmiCachedTemp;
        private static DateTime _wmiLastQueryUtc = DateTime.MinValue;
        private static bool _wmiUnsupported;

        /// <summary>
        /// Secours indépendant du driver ring0 : lit la zone thermique ACPI exposée par le BIOS
        /// (MSAcpi_ThermalZoneTemperature, en dixièmes de Kelvin). Throttlé à 1 requête / 5 s.
        /// Beaucoup de cartes mères de bureau ne l'implémentent pas → renvoie null.
        /// </summary>
        private static float? ReadWmiThermalZone()
        {
            if (_wmiUnsupported) return null;
            if ((DateTime.UtcNow - _wmiLastQueryUtc).TotalSeconds < 5)
                return _wmiCachedTemp;
            _wmiLastQueryUtc = DateTime.UtcNow;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"root\WMI", "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                float? best = null;
                foreach (ManagementBaseObject obj in searcher.Get())
                {
                    if (obj["CurrentTemperature"] is null) continue;
                    double tenthsKelvin = System.Convert.ToDouble(obj["CurrentTemperature"]);
                    double celsius = tenthsKelvin / 10.0 - 273.15;
                    if (celsius > 0 && celsius < 130)
                        best = best is null ? (float)celsius : Math.Max(best.Value, (float)celsius);
                }
                _wmiCachedTemp = best;
                return best;
            }
            catch
            {
                // Non supporté par ce BIOS — on n'essaiera plus.
                _wmiUnsupported = true;
                _wmiCachedTemp = null;
                return null;
            }
        }

        /// <summary>
        /// Secours Ryzen : cherche une température "CPU" exposée par la carte mère (Super I/O,
        /// sous-puces). Renvoie la 1re valeur valide (> 0 et &lt; 130°C), sinon null.
        /// </summary>
        private static float? ReadMotherboardCpuTemp(IHardware hw)
        {
            float? best = null;
            void Scan(IHardware h)
            {
                foreach (var s in h.Sensors)
                {
                    if (s.SensorType != SensorType.Temperature) continue;
                    if (s.Value is not float v || v <= 0f || v > 130f) continue;
                    string n = s.Name;
                    if (n.Contains("CPU") || n.Contains("Tctl") || n.Contains("Tdie"))
                        best = best is null ? v : Math.Max(best.Value, v);
                }
                foreach (var sub in h.SubHardware) Scan(sub);
            }
            Scan(hw);
            return best;
        }

        private static void ReadGpu(IHardware hw, HardwareSnapshot snap)
        {
            snap.GpuName = hw.Name;
            foreach (var s in hw.Sensors)
            {
                if (s.Value is not float v) continue;
                switch (s.SensorType)
                {
                    case SensorType.Temperature when s.Name.Contains("Core") || s.Name.Contains("GPU"):
                        snap.GpuTempC = snap.GpuTempC is null ? v : Math.Max(snap.GpuTempC.Value, v);
                        break;
                    case SensorType.Load when s.Name == "GPU Core":
                        snap.GpuLoadPct = v;
                        break;
                    case SensorType.SmallData when s.Name.Contains("Memory Used"):
                        snap.GpuVramUsedMb = v;
                        break;
                    case SensorType.SmallData when s.Name.Contains("Memory Total"):
                        snap.GpuVramTotalMb = v;
                        break;
                }
            }
        }

        private static void ReadStorage(IHardware hw, HardwareSnapshot snap)
        {
            foreach (var s in hw.Sensors)
            {
                if (s.Value is not float v) continue;
                switch (s.SensorType)
                {
                    case SensorType.Throughput when s.Name.Contains("Read"):
                        snap.DiskReadKBs = (snap.DiskReadKBs ?? 0) + v / 1024f;
                        break;
                    case SensorType.Throughput when s.Name.Contains("Write"):
                        snap.DiskWriteKBs = (snap.DiskWriteKBs ?? 0) + v / 1024f;
                        break;
                    case SensorType.Temperature:
                        snap.DiskTempC = snap.DiskTempC is null ? v : Math.Max(snap.DiskTempC.Value, v);
                        break;
                }
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            lock (_lock)
            {
                try { _computer?.Close(); }
                catch { /* best-effort */ }
                _computer = null;
            }
        }
    }
}
