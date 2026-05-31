using System.IO;
using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Services
{
    /// <summary>
    /// Paramètres utilisateur persistants, sauvegardés en JSON dans AppData.
    /// </summary>
    public class AppSettings : INotifyPropertyChanged
    {
        private bool _confirmBeforeRun      = true;
        private bool _autoRestorePoint      = false;
        private bool _showAdminBadge        = true;
        private bool _autoScrollTerminal    = true;
        private bool _showRebootWarning     = true;
        private bool _simulationMode        = false;
        private bool _showAdvancedOptimizations = false;
        private bool _enableOptimizationBenchmarks = true;
        private bool _autoSaveLogs          = true;
        private string _theme               = "Dark";
        private string _lastModule          = "Dashboard";
        /// <summary>v2.2.0 : "auto" (système), "fr" ou "en".</summary>
        private string _language            = "auto";

        /// <summary>Demander confirmation avant d'exécuter une optimisation.</summary>
        public bool ConfirmBeforeRun
        {
            get => _confirmBeforeRun;
            set { _confirmBeforeRun = value; OnPropertyChanged(); }
        }

        /// <summary>Créer automatiquement un point de restauration avant chaque optimisation admin.</summary>
        public bool AutoRestorePoint
        {
            get => _autoRestorePoint;
            set { _autoRestorePoint = value; OnPropertyChanged(); }
        }

        /// <summary>Afficher le badge "Admin" sur les optimisations qui le nécessitent.</summary>
        public bool ShowAdminBadge
        {
            get => _showAdminBadge;
            set { _showAdminBadge = value; OnPropertyChanged(); }
        }

        /// <summary>Scroll automatique du terminal vers le bas.</summary>
        public bool AutoScrollTerminal
        {
            get => _autoScrollTerminal;
            set { _autoScrollTerminal = value; OnPropertyChanged(); }
        }

        /// <summary>Afficher un avertissement quand un redémarrage est requis.</summary>
        public bool ShowRebootWarning
        {
            get => _showRebootWarning;
            set { _showRebootWarning = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Mode simulation : les optimisations critiques sont simulées sans être exécutées.
        /// </summary>
        public bool SimulationMode
        {
            get => _simulationMode;
            set { _simulationMode = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Affiche les optimisations avancées (risque plus élevé ou gain incertain).
        /// </summary>
        public bool ShowAdvancedOptimizations
        {
            get => _showAdvancedOptimizations;
            set { _showAdvancedOptimizations = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Active la capture benchmark avant/après pour les optimisations majeures.
        /// </summary>
        public bool EnableOptimizationBenchmarks
        {
            get => _enableOptimizationBenchmarks;
            set { _enableOptimizationBenchmarks = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Sauvegarde automatique des logs dans Documents\Klyr\Logs\Klyr_YYYYMMDD.log
        /// (fichier journalier, append-only, conservé en cas de crash).
        /// </summary>
        public bool AutoSaveLogs
        {
            get => _autoSaveLogs;
            set { _autoSaveLogs = value; OnPropertyChanged(); }
        }

        public string Theme
        {
            get => _theme;
            set { _theme = value; OnPropertyChanged(); }
        }

        public string LastModule
        {
            get => _lastModule;
            set { _lastModule = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// Langue d'affichage. Valeurs valides : "auto" (suit le système), "fr", "en".
        /// Le changement nécessite un redémarrage de l'app pour prendre effet sur toutes les vues.
        /// </summary>
        public string Language
        {
            get => _language;
            set { _language = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    /// <summary>
    /// Charge et sauvegarde les paramètres dans %AppData%\Klyr\settings.json
    /// FIX P2-04: Logging des erreurs au lieu de catches silencieux.
    /// </summary>
    public static class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Klyr", "settings.json");

        private static AppSettings? _current;
        public static AppSettings Current => _current ??= Load();

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch (Exception ex)
            {
                // FIX P2-04: Logger au lieu de silencieux
                LogService.Instance.Warn($"Impossible de charger les paramètres : {ex.Message}. Utilisation des valeurs par défaut.", "Settings");
            }
            return new AppSettings();
        }

        public static void Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(dir);
                string json = JsonSerializer.Serialize(Current, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                // FIX P2-04: Logger au lieu de silencieux
                LogService.Instance.Error($"Impossible de sauvegarder les paramètres : {ex.Message}", "Settings");
            }
        }
    }
}
