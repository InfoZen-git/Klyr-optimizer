using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Models
{
    public enum OptimizationPurpose
    {
        Performance,
        Maintenance,
        Troubleshooting,
        SecurityPrivacy
    }

    /// <summary>
    /// Représente une optimisation individuelle dans un module.
    /// </summary>
    public class OptimizationItem : INotifyPropertyChanged
    {
        private bool   _isEnabled;
        private bool   _isRunning;
        private string _status   = "Prêt";
        private int    _progress = 0;   // 0–100 pour la barre de progression fictive
        private int    _confidenceScore = 60;

        public string Id             { get; set; } = string.Empty;
        public string Name           { get; set; } = string.Empty;
        public string Description    { get; set; } = string.Empty;
        public string Category       { get; set; } = string.Empty;
        public bool   RequiresAdmin  { get; set; } = false;
        public bool   RequiresReboot { get; set; } = false;
        public bool   IsAdvanced     { get; set; } = false;
        public bool   IsBenchmarkCandidate { get; set; } = false;
        public OptimizationPurpose Purpose { get; set; } = OptimizationPurpose.Performance;

        /// <summary>Action à exécuter – retourne le message de résultat.</summary>
        public Func<Task<string>>? Action { get; set; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { _isEnabled = value; OnPropertyChanged(); }
        }

        public bool IsRunning
        {
            get => _isRunning;
            set { _isRunning = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressVisible)); }
        }

        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); }
        }

        /// <summary>Progression fictive 0–100 affichée pendant l'exécution.</summary>
        public int Progress
        {
            get => _progress;
            set { _progress = Math.Clamp(value, 0, 100); OnPropertyChanged(); OnPropertyChanged(nameof(ProgressStr)); }
        }

        /// <summary>
        /// Confiance de gain recommandée par OPTIMUS (0-100).
        /// </summary>
        public int ConfidenceScore
        {
            get => _confidenceScore;
            set
            {
                _confidenceScore = Math.Clamp(value, 0, 100);
                OnPropertyChanged();
                OnPropertyChanged(nameof(ConfidenceLabel));
                OnPropertyChanged(nameof(ConfidenceBadgeBackground));
                OnPropertyChanged(nameof(ConfidenceBadgeForeground));
            }
        }

        public string ProgressStr    => $"{_progress}%";
        public bool   ProgressVisible => _isRunning;
        public string ConfidenceLabel => $"Confiance {ConfidenceScore}%";

        public string PurposeLabel => Purpose switch
        {
            OptimizationPurpose.Performance    => "Perf",
            OptimizationPurpose.Maintenance    => "Maintenance",
            OptimizationPurpose.Troubleshooting => "Dépannage",
            OptimizationPurpose.SecurityPrivacy => "Sécurité",
            _ => "Perf"
        };

        public string PurposeBadgeBackground => Purpose switch
        {
            OptimizationPurpose.Performance     => "#1E2E4A",
            OptimizationPurpose.Maintenance     => "#1E3A2A",
            OptimizationPurpose.Troubleshooting => "#3F2E1C",
            OptimizationPurpose.SecurityPrivacy => "#3A1F2A",
            _ => "#2A2A2A"
        };

        public string PurposeBadgeForeground => Purpose switch
        {
            OptimizationPurpose.Performance     => "#6A9FD8",
            OptimizationPurpose.Maintenance     => "#67C08A",
            OptimizationPurpose.Troubleshooting => "#D0A46A",
            OptimizationPurpose.SecurityPrivacy => "#D07AA0",
            _ => "#AAAAAA"
        };

        public string ConfidenceBadgeBackground => ConfidenceScore switch
        {
            >= 75 => "#1E3A2A",
            >= 45 => "#3A2F1E",
            _     => "#3A1F1F"
        };

        public string ConfidenceBadgeForeground => ConfidenceScore switch
        {
            >= 75 => "#67C08A",
            >= 45 => "#D0A46A",
            _     => "#D87A7A"
        };

        public string StatusColor => _status switch
        {
            "Terminé"   => "#00FF88",
            "Erreur"    => "#FF3B3B",
            "En cours…" => "#00D4FF",
            _           => "#3D4459"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
