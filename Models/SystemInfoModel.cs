using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace InfoZen.Models
{
    /// <summary>
    /// Données système affichées en temps réel sur le tableau de bord.
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

        public string CpuUsageStr  => $"{_cpuUsage:F1}%";
        public string RamPercentStr => $"{_ramPercent:F1}%";
        public string RamInfoStr   => $"{_ramUsedGb:F1} / {_ramTotalGb:F1} Go";
        public string DiskInfoStr  => $"{_diskFreeGb} Go libres / {_diskTotalGb} Go";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
