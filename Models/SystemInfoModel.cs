using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Models
{
    /// <summary>
    /// Données système affichées en temps réel sur le tableau de bord.
    /// v2.3.0 : étendu avec capteurs matériels (températures, GPU, vitesses disque) + Performance Score.
    /// </summary>
    public class SystemInfoModel : INotifyPropertyChanged
    {
        private string _cpuName     = "—";
        private float  _cpuUsage    = 0f;
        private float  _ramUsedGb   = 0f;
        private float  _ramTotalGb  = 0f;
        private float  _ramPercent  = 0f;
        private string _osName      = "—";
        private string _uptime      = "—";
        private string _publicIp    = "—";
        private string _machineName = "—";
        private long   _diskFreeGb  = 0;
        private long   _diskTotalGb = 0;

        // v2.3.0 — Capteurs matériels (nullable : indisponible sans admin / capteur absent)
        private float? _cpuTempC;
        private float? _gpuTempC;
        private float? _gpuLoadPct;
        private string _gpuName     = "—";
        private float? _diskReadKBs;
        private float? _diskWriteKBs;
        private float? _diskTempC;
        private int    _performanceScore = 0;

        public string CpuName     { get => _cpuName;     set { _cpuName = value;     OnPropertyChanged(); } }
        public float  CpuUsage    { get => _cpuUsage;    set { _cpuUsage = value;    OnPropertyChanged(); OnPropertyChanged(nameof(CpuUsageStr)); } }
        public float  RamUsedGb   { get => _ramUsedGb;   set { _ramUsedGb = value;   OnPropertyChanged(); } }
        public float  RamTotalGb  { get => _ramTotalGb;  set { _ramTotalGb = value;  OnPropertyChanged(); } }
        public float  RamPercent  { get => _ramPercent;  set { _ramPercent = value;  OnPropertyChanged(); OnPropertyChanged(nameof(RamPercentStr)); } }
        public string OsName      { get => _osName;      set { _osName = value;      OnPropertyChanged(); } }
        public string Uptime      { get => _uptime;      set { _uptime = value;      OnPropertyChanged(); } }
        public string PublicIp    { get => _publicIp;    set { _publicIp = value;    OnPropertyChanged(); } }
        public string MachineName { get => _machineName; set { _machineName = value; OnPropertyChanged(); } }
        public long   DiskFreeGb  { get => _diskFreeGb;  set { _diskFreeGb = value;  OnPropertyChanged(); } }
        public long   DiskTotalGb { get => _diskTotalGb; set { _diskTotalGb = value; OnPropertyChanged(); } }

        // ── Capteurs v2.3.0 ──
        public float? CpuTempC
        {
            get => _cpuTempC;
            set { _cpuTempC = value; OnPropertyChanged(); OnPropertyChanged(nameof(CpuTempStr)); OnPropertyChanged(nameof(HasCpuTemp)); }
        }
        public float? GpuTempC
        {
            get => _gpuTempC;
            set { _gpuTempC = value; OnPropertyChanged(); OnPropertyChanged(nameof(GpuTempStr)); OnPropertyChanged(nameof(HasGpu)); }
        }
        public float? GpuLoadPct
        {
            get => _gpuLoadPct;
            set { _gpuLoadPct = value; OnPropertyChanged(); OnPropertyChanged(nameof(GpuLoadStr)); OnPropertyChanged(nameof(HasGpu)); }
        }
        public string GpuName
        {
            get => _gpuName;
            set { _gpuName = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasGpu)); }
        }
        public float? DiskReadKBs
        {
            get => _diskReadKBs;
            set { _diskReadKBs = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskSpeedStr)); }
        }
        public float? DiskWriteKBs
        {
            get => _diskWriteKBs;
            set { _diskWriteKBs = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskSpeedStr)); }
        }
        public float? DiskTempC
        {
            get => _diskTempC;
            set { _diskTempC = value; OnPropertyChanged(); OnPropertyChanged(nameof(DiskTempStr)); }
        }

        /// <summary>Performance Score 0-100 (calculé par PerformanceScoreService).</summary>
        public int PerformanceScore
        {
            get => _performanceScore;
            set
            {
                _performanceScore = System.Math.Clamp(value, 0, 100);
                OnPropertyChanged();
                OnPropertyChanged(nameof(PerformanceScoreStr));
                OnPropertyChanged(nameof(PerformanceScoreColor));
                OnPropertyChanged(nameof(PerformanceScoreLabel));
            }
        }

        // ── Strings d'affichage ──
        public string CpuUsageStr   => $"{_cpuUsage:F1}%";
        public string RamPercentStr => $"{_ramPercent:F1}%";
        public string RamInfoStr    => $"{_ramUsedGb:F1} / {_ramTotalGb:F1} Go";
        public string DiskInfoStr   => $"{_diskFreeGb} Go libres / {_diskTotalGb} Go";

        public bool   HasCpuTemp    => _cpuTempC.HasValue;
        public string CpuTempStr    => _cpuTempC.HasValue ? $"{_cpuTempC.Value:F0}°C" : "—";
        public bool   HasGpu        => _gpuLoadPct.HasValue || _gpuTempC.HasValue || _gpuName != "—";
        public string GpuTempStr    => _gpuTempC.HasValue ? $"{_gpuTempC.Value:F0}°C" : "—";
        public string GpuLoadStr    => _gpuLoadPct.HasValue ? $"{_gpuLoadPct.Value:F0}%" : "—";
        public string DiskTempStr   => _diskTempC.HasValue ? $"{_diskTempC.Value:F0}°C" : "—";

        public string DiskSpeedStr
        {
            get
            {
                string Fmt(float? kbs) => kbs switch
                {
                    null            => "—",
                    >= 1024         => $"{kbs.Value / 1024f:F1} Mo/s",
                    _               => $"{kbs.Value:F0} Ko/s"
                };
                return $"R {Fmt(_diskReadKBs)}  ·  W {Fmt(_diskWriteKBs)}";
            }
        }

        public string PerformanceScoreStr => _performanceScore.ToString();

        public string PerformanceScoreLabel => _performanceScore switch
        {
            >= 80 => Klyr.Resources.Strings.Score_Excellent,
            >= 60 => Klyr.Resources.Strings.Score_Good,
            >= 40 => Klyr.Resources.Strings.Score_Average,
            >= 20 => Klyr.Resources.Strings.Score_Low,
            _     => Klyr.Resources.Strings.Score_Critical
        };

        public string PerformanceScoreColor => _performanceScore switch
        {
            >= 80 => "#4CAF50",   // vert
            >= 60 => "#8BC34A",   // vert clair
            >= 40 => "#E8A838",   // orange
            >= 20 => "#FF7043",   // orange foncé
            _     => "#E05252"    // rouge
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
