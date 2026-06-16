using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Models
{
    /// <summary>
    /// v2.3.0 — Représente un programme installé (lu depuis le registre Uninstall).
    /// </summary>
    public class InstalledProgram : INotifyPropertyChanged
    {
        public string Name            { get; set; } = "";
        public string Version         { get; set; } = "";
        public string Publisher       { get; set; } = "";
        public string InstallLocation { get; set; } = "";
        public string UninstallString { get; set; } = "";
        public string QuietUninstallString { get; set; } = "";
        public long   EstimatedSizeKb { get; set; }
        public string RegistryKeyPath { get; set; } = "";
        public bool   IsWow64         { get; set; }

        public string SizeStr => EstimatedSizeKb switch
        {
            <= 0          => "—",
            >= 1048576    => $"{EstimatedSizeKb / 1048576.0:F1} Go",
            >= 1024       => $"{EstimatedSizeKb / 1024.0:F0} Mo",
            _             => $"{EstimatedSizeKb} Ko"
        };

        public string PublisherStr => string.IsNullOrWhiteSpace(Publisher) ? "—" : Publisher;
        public string VersionStr   => string.IsNullOrWhiteSpace(Version) ? "" : $"v{Version}";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
