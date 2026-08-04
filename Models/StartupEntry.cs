using System.ComponentModel;
using System.Runtime.CompilerServices;
using Klyr.Resources;

namespace Klyr.Models
{
    /// <summary>
    /// v2.5.0 (fix) — Programme lancé au démarrage de Windows.
    /// L'état activé/désactivé est déterminé par la clé <c>StartupApproved</c> exactement
    /// comme le Gestionnaire des tâches Windows (et non par la simple présence dans Run).
    /// </summary>
    public class StartupEntry : INotifyPropertyChanged
    {
        public string        Name         { get; set; } = "";  // nom de valeur (registre) ou nom du .lnk (dossier)
        public string        Command      { get; set; } = "";
        public string        Location     { get; set; } = "";  // libellé lisible de la source
        public StartupSource Source       { get; set; }
        public string        ShortcutPath { get; set; } = "";  // pour les entrées « dossier démarrage »

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusStr)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public string StatusStr   => _isEnabled ? Strings.Startup_Enabled : Strings.Startup_Disabled;
        public string StatusColor => _isEnabled ? "#4CAF50" : "#8A8A8A";
        public string SourceStr   => Location;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    /// <summary>Emplacement source d'une entrée de démarrage.</summary>
    public enum StartupSource
    {
        RegistryHKCU,     // HKCU\...\Run
        RegistryHKLM,     // HKLM\...\Run (64-bit)
        RegistryHKLM32,   // HKLM\...\WOW6432Node\...\Run (32-bit)
        FolderUser,       // dossier Démarrage de l'utilisateur
        FolderCommon      // dossier Démarrage commun (tous les utilisateurs)
    }
}
