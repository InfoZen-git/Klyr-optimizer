using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Models
{
    /// <summary>
    /// v2.3.0 — Paquet avec mise à jour disponible (winget upgrade).
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
