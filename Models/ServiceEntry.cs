using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Models
{
    /// <summary>
    /// v2.4.0 — Service Windows non essentiel proposé au toggle dans le
    /// Gestionnaire de services. Désactivation réversible : le type de démarrage
    /// d'origine (<see cref="DefaultStartMode"/>) est restauré à la réactivation.
    /// </summary>
    public class ServiceEntry : INotifyPropertyChanged
    {
        public string Name        { get; set; } = "";   // nom court du service (ex. DiagTrack)
        public string DisplayName { get; set; } = "";   // nom lisible fourni par Windows
        public string Description { get; set; } = "";   // explication localisée (ce que ça fait)
        /// <summary>Type de démarrage à restaurer si l'utilisateur réactive (Automatic / Manual).</summary>
        public string DefaultStartMode { get; set; } = "Manual";

        private bool _isEnabled = true;   // false = StartMode Disabled
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusStr)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public bool IsRunning { get; set; }

        public string StatusStr   => _isEnabled ? "Activé" : "Désactivé";
        public string StatusColor => _isEnabled ? "#4CAF50" : "#8A8A8A";

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
