using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Models
{
    /// <summary>
    /// v2.3.0 — Programme lancé au démarrage de Windows.
    /// Source : registre Run (HKLM/HKCU) ou dossier Startup.
    /// </summary>
    public class StartupEntry : INotifyPropertyChanged
    {
        public string Name      { get; set; } = "";
        public string Command   { get; set; } = "";
        public string Location  { get; set; } = "";  // libellé lisible de la source
        public StartupSource Source { get; set; }
        public string RegistryKeyPath { get; set; } = ""; // pour les sources registre
        public string ShortcutPath    { get; set; } = ""; // pour les sources dossier Startup

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusStr)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public string StatusStr   => _isEnabled ? "Activé" : "Désactivé";
        public string StatusColor => _isEnabled ? "#4CAF50" : "#8A8A8A";
        public string SourceStr   => Location;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public enum StartupSource
    {
        RegistryHKLM,
        RegistryHKCU,
        StartupFolder
    }
}
