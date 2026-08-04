using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Models
{
    /// <summary>État d'un paquet pendant la mise à jour en lot.</summary>
    public enum PackageStatus { Pending, Updating, Done, Failed }

    /// <summary>
    /// v2.3.0 — Paquet avec mise à jour disponible (winget upgrade).
    /// v2.5.0 (fix) — Suivi d'état par ligne (indicateur ✓/✕/… pendant la mise à jour en lot).
    /// </summary>
    public class UpgradablePackage : INotifyPropertyChanged
    {
        public string Name           { get; set; } = "";
        public string Id             { get; set; } = "";
        public string CurrentVersion { get; set; } = "";
        public string AvailableVersion { get; set; } = "";

        public string VersionTransition => $"{CurrentVersion}  →  {AvailableVersion}";

        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        private PackageStatus _status = PackageStatus.Pending;
        public PackageStatus Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusGlyph));
                OnPropertyChanged(nameof(StatusColor));
            }
        }

        /// <summary>Indicateur visuel par ligne : … en cours, ✓ terminé, ✕ échec.</summary>
        public string StatusGlyph => _status switch
        {
            PackageStatus.Updating => "…",
            PackageStatus.Done     => "✓",
            PackageStatus.Failed   => "✕",
            _                      => ""
        };

        public string StatusColor => _status switch
        {
            PackageStatus.Done     => "#4CAF50",
            PackageStatus.Failed   => "#FF5252",
            PackageStatus.Updating => "#00D4FF",
            _                      => "#8A8A8A"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
