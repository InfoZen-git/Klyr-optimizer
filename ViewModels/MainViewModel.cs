using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using InfoZen.Commands;
using InfoZen.Models;
using InfoZen.Services;

namespace InfoZen.ViewModels
{
    /// <summary>
    /// ViewModel principal de l'application.
    /// FIX P1-05: Implémente IDisposable pour cleanup propre du timer.
    /// FIX P0-02: Garde admin stricte avant exécution.
    /// </summary>
    public class MainViewModel : INotifyPropertyChanged, IDisposable
    {
        // ─────────────────────── NAVIGATION ────────────────────────────────────
        private string _currentPage = "";
        public  string CurrentPage
        {
            get => _currentPage;
            set
            {
                string nextPage = NormalizeStartupPage(value);
                if (_currentPage == nextPage) return;

                _currentPage = nextPage;
                SettingsService.Current.LastModule = nextPage;

                OnPropertyChanged();
                OnPropertyChanged(nameof(PageTitle));
                LoadPageOptimizations();
            }
        }

        public string PageTitle => CurrentPage switch
        {
            "Dashboard" => "Tableau de bord",
            "Gaming"    => "Gaming / FPS",
            "OldPC"     => "Vieux PC",
            "Cleaning"  => "Nettoyage",
            "Network"   => "Réseau",
            "Terminal"  => "Terminal",
            "Legal"     => "Mentions légales",
            _           => CurrentPage
        };

        // ─────────────────────── OPTIMISATIONS ─────────────────────────────────
        public ObservableCollection<OptimizationItem> CurrentOptimizations { get; } = new();

        // ─────────────────────── INFOS SYSTÈME ─────────────────────────────────
        public SystemInfoModel SystemInfo { get; } = new();

        private float _cpuUsage;
        public  float CpuUsage
        {
            get => _cpuUsage;
            set { _cpuUsage = value; OnPropertyChanged(); OnPropertyChanged(nameof(CpuUsageStr)); }
        }
        public string CpuUsageStr => $"{_cpuUsage:F0}%";

        // ─────────────────────── TERMINAL ──────────────────────────────────────
        public LogService Log => LogService.Instance;

        private string _terminalOutput = "";
        public  string TerminalOutput
        {
            get => _terminalOutput;
            set { _terminalOutput = value; OnPropertyChanged(); }
        }

        // ─────────────────────── ÉTAT ──────────────────────────────────────────
        private bool _isBusy;
        public  bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        private string _statusMessage = "Prêt";
        public  string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private int _optimizationsRun = 0;
        public  int OptimizationsRun
        {
            get => _optimizationsRun;
            set { _optimizationsRun = value; OnPropertyChanged(); }
        }

        // ─────────────────────── COMMANDS ──────────────────────────────────────
        public AsyncRelayCommand RunOptimizationCommand     { get; }
        public AsyncRelayCommand RunAllOptimizationsCommand { get; }
        public AsyncRelayCommand CreateRestorePointCommand  { get; }
        public RelayCommand      StopRunAllCommand          { get; }
        public RelayCommand      ExportLogsCommand          { get; }
        public RelayCommand      ClearLogsCommand           { get; }
        public RelayCommand      NavigateCommand            { get; }

        private readonly System.Timers.Timer _refreshTimer;
        private bool _disposed;
        private PropertyChangedEventHandler? _logPropertyChangedHandler;
        private CancellationTokenSource? _runAllCts;
        private bool _isRunAllInProgress;
        private bool _suppressPerItemConfirmation;
        public bool IsRunAllInProgress
        {
            get => _isRunAllInProgress;
            private set { _isRunAllInProgress = value; OnPropertyChanged(); }
        }

        private static readonly HashSet<string> CriticalOptimizationIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "net_reset",
            "net_reset_firewall",
            "net_telemetry_block"
        };

        public MainViewModel()
        {
            RefreshSystemInfo();

            RunOptimizationCommand     = new AsyncRelayCommand(RunOptimizationAsync);
            RunAllOptimizationsCommand = new AsyncRelayCommand(RunAllOptimizationsAsync);
            CreateRestorePointCommand  = new AsyncRelayCommand(CreateRestorePointAsync);
            StopRunAllCommand          = new RelayCommand(StopRunAllOptimizations, _ => _runAllCts != null && !_runAllCts.IsCancellationRequested);
            ExportLogsCommand          = new RelayCommand(ExportLogs);
            ClearLogsCommand           = new RelayCommand(() => Log.Clear());
            NavigateCommand            = new RelayCommand(p => CurrentPage = NormalizeStartupPage(p?.ToString()));

            CurrentPage = NormalizeStartupPage(SettingsService.Current.LastModule);

            _refreshTimer = new System.Timers.Timer(2000);
            _refreshTimer.Elapsed += (_, _) => RefreshRealtimeStats();
            _refreshTimer.Start();

            // FIX P1-05: Garder une référence pour pouvoir désabonner
            _logPropertyChangedHandler = (_, e) =>
            {
                if (e.PropertyName == nameof(LogService.TerminalText))
                    TerminalOutput = Log.TerminalText;
            };
            Log.PropertyChanged += _logPropertyChangedHandler;

            Log.Info("InfoZen v2.1 démarré.", "Système");
            Log.Info("InfoZen fonctionne exclusivement en local. Aucune donnée personnelle n'est collectée.", "RGPD");
        }

        // ─────────────────────── NAVIGATION ────────────────────────────────────
        // FIX: cache des items par page pour préserver l'état (IsRunning, Progress, Status)
        // quand l'utilisateur navigue entre pages pendant qu'une optimisation tourne.
        private readonly Dictionary<string, List<OptimizationItem>> _pageOptimizationsCache = new();

        private void LoadPageOptimizations()
        {
            if (!_pageOptimizationsCache.TryGetValue(CurrentPage, out var items))
            {
                items = CurrentPage switch
                {
                    "Gaming"   => GamingOptimizations.GetOptimizations(),
                    "OldPC"    => OldPcOptimizations.GetOptimizations(),
                    "Cleaning" => CleaningOptimizations.GetOptimizations(),
                    "Network"  => NetworkOptimizations.GetOptimizations(),
                    _          => new List<OptimizationItem>()
                };
                _pageOptimizationsCache[CurrentPage] = items;
            }

            var filtered = SettingsService.Current.ShowAdvancedOptimizations
                ? items
                : items.Where(i => !i.IsAdvanced).ToList();

            CurrentOptimizations.Clear();
            foreach (var item in filtered) CurrentOptimizations.Add(item);
        }

        public void ReloadCurrentOptimizations() => LoadPageOptimizations();

        // ─────────────────────── RUN OPTIMISATION ──────────────────────────────
        private async Task RunOptimizationAsync(object? param)
        {
            var item = param as OptimizationItem ?? null;
            if (item?.Action == null) return;

            if (_runAllCts?.IsCancellationRequested == true)
            {
                Log.Warn($"⚠ {item.Name} ignoré (arrêt global demandé).", item.Category);
                return;
            }

            if (IsCriticalOptimization(item.Id))
            {
                if (SettingsService.Current.SimulationMode)
                {
                    item.Progress = 100;
                    item.Status = "Simulé";
                    string simulation = BuildCriticalSimulationMessage(item);
                    Log.Warn($"[SIMULATION] {simulation}", item.Category);
                    TerminalOutput = Log.TerminalText;
                    OptimizationsRun++;
                    return;
                }

                var criticalConfirm = MessageBox.Show(
                    $"⚠ L'optimisation « {item.Name} » peut modifier des paramètres système critiques.\n\n" +
                    "InfoZen créera une sauvegarde dédiée avant exécution.\n\n" +
                    "Confirmez-vous cette action ?",
                    "Action critique – InfoZen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (criticalConfirm != MessageBoxResult.Yes)
                {
                    Log.Warn($"⚠ {item.Name} annulé (action critique non confirmée).", item.Category);
                    return;
                }
            }

            // FIX P0-02: Garde admin stricte - bloquer si RequiresAdmin et non admin
            if (item.RequiresAdmin && !AdminChecker.IsRunningAsAdmin())
            {
                var result = MessageBox.Show(
                    $"L'optimisation « {item.Name} » nécessite les droits administrateur.\n\n" +
                    "Voulez-vous relancer InfoZen en tant qu'administrateur ?",
                    "Droits insuffisants – InfoZen",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = Environment.ProcessPath ?? "InfoZen.exe",
                            UseShellExecute = true,
                            Verb = "runas"
                        };
                        System.Diagnostics.Process.Start(psi);
                        Application.Current.Shutdown();
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"Impossible de relancer en admin : {ex.Message}", "Système");
                    }
                }
                else
                {
                    Log.Warn($"⚠ {item.Name} ignoré (droits admin requis).", item.Category);
                }
                return;
            }

            // Confirmation avant exécution
            if (SettingsService.Current.ConfirmBeforeRun && !_suppressPerItemConfirmation)
            {
                var confirm = MessageBox.Show(
                    $"Exécuter l'optimisation :\n\n« {item.Name} »\n\n{item.Description}",
                    "Confirmation – InfoZen",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.OK) return;
            }

            // Point de restauration automatique avant les actions admin
            if (SettingsService.Current.AutoRestorePoint && item.RequiresAdmin)
            {
                Log.Info("Création d'un point de restauration automatique…", "Système");
                StatusMessage = "Création du point de restauration…";
                var restoreResult = await SystemService.CreateRestorePointAsync($"InfoZen – avant : {item.Name}");
                if (!restoreResult.Success)
                {
                    Log.Warn($"Point de restauration non créé : {restoreResult.DisplayMessage}", "Système");
                }
            }

            if (!IsRunAllInProgress)
                IsBusy = true;
            item.IsRunning = true;
            item.Progress  = 0;
            item.Status    = "En cours…";
            StatusMessage  = $"⟳  {item.Name}";
            Log.Info($"▶ Démarrage : {item.Name}", item.Category);

            OptimizationBenchmarkSnapshot? benchmarkBefore = null;
            bool benchmarkEnabled = SettingsService.Current.EnableOptimizationBenchmarks && item.IsBenchmarkCandidate;
            if (benchmarkEnabled)
            {
                try
                {
                    benchmarkBefore = await OptimizationBenchmarkService.CaptureAsync(item);
                    Log.Info($"📏 Baseline capturée pour {item.Name}.", "Benchmark");
                }
                catch (Exception ex)
                {
                    benchmarkEnabled = false;
                    Log.Warn($"Benchmark baseline ignoré ({item.Name}) : {ex.Message}", "Benchmark");
                }
            }

            var benchmarkWatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Lance l'action AVEC progression animée (suggestion communauté)
                string result = await ProgressHelper.RunWithProgressAsync(
                    item,
                    item.Action,
                    estimatedMs: EstimateMs(item.Id)
                );

                item.Progress = 100;
                item.Status   = result.StartsWith("❌") || result.Contains("[ERR]") ? "Erreur" : "Terminé";

                if (result.StartsWith("❌") || result.StartsWith("[ERR]"))
                    Log.Error(result, item.Category);
                else
                    Log.Success(result, item.Category);

                benchmarkWatch.Stop();
                if (benchmarkEnabled && benchmarkBefore != null)
                {
                    try
                    {
                        var benchmarkAfter = await OptimizationBenchmarkService.CaptureAsync(item);
                        string benchmarkSummary = OptimizationBenchmarkService.BuildSummary(
                            item,
                            benchmarkBefore,
                            benchmarkAfter,
                            benchmarkWatch.Elapsed);
                        Log.Info(benchmarkSummary, "Benchmark");
                    }
                    catch (Exception ex)
                    {
                        Log.Warn($"Benchmark final ignoré ({item.Name}) : {ex.Message}", "Benchmark");
                    }
                }

                TerminalOutput = Log.TerminalText;
                OptimizationsRun++;

                if (item.RequiresReboot && SettingsService.Current.ShowRebootWarning)
                {
                    Log.Warn("Redémarrage requis pour appliquer les changements.", item.Category);
                    MessageBox.Show(
                        $"L'optimisation « {item.Name} » nécessite un redémarrage.",
                        "Redémarrage requis – InfoZen",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                benchmarkWatch.Stop();
                item.Status   = "Erreur";
                item.Progress = 0;
                Log.Error($"Exception : {ex.Message}", item.Category);
            }
            finally
            {
                item.IsRunning = false;
                if (!IsRunAllInProgress)
                {
                    IsBusy = false;
                    StatusMessage = "Prêt";
                }
            }
        }

        /// <summary>Estime la durée de chaque optimisation pour calibrer la progression fictive.</summary>
        private static int EstimateMs(string id) => id switch
        {
            "clean_repair"    => 45000,  // SFC + DISM = long
            "clean_antivirus" => 30000,  // Scan = long
            "clean_disk"      => 8000,
            "oldpc_defrag"    => 20000,
            "oldpc_ram"       => 3000,
            "net_speed_test"  => 10000,
            _                 => 2500    // Par défaut 2.5s
        };

        private async Task RunAllOptimizationsAsync(object? _)
        {
            if (!CurrentOptimizations.Any()) return;
            if (IsRunAllInProgress) return;

            if (SettingsService.Current.ConfirmBeforeRun)
            {
                var batchConfirm = MessageBox.Show(
                    $"Exécuter {CurrentOptimizations.Count} optimisations de « {CurrentPage} » ?\n\n" +
                    "Les optimisations seront priorisées automatiquement.",
                    "Exécution groupée – InfoZen",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);

                if (batchConfirm != MessageBoxResult.OK)
                    return;
            }

            _runAllCts = new CancellationTokenSource();
            IsRunAllInProgress = true;
            _suppressPerItemConfirmation = true;
            IsBusy = true;
            CommandManager.InvalidateRequerySuggested();

            var prioritizedItems = CurrentOptimizations
                .OrderBy(GetRunAllPriority)
                .ThenBy(i => EstimateMs(i.Id))
                .ToList();

            Log.Info($"=== Exécution de toutes les optimisations ({CurrentPage}) ===", "Système");

            try
            {
                foreach (var item in prioritizedItems)
                {
                    if (_runAllCts.IsCancellationRequested)
                    {
                        Log.Warn("⚠ Exécution groupée interrompue par l'utilisateur.", "Système");
                        break;
                    }

                    await RunOptimizationAsync(item);
                }

                if (!_runAllCts.IsCancellationRequested)
                    Log.Info("=== Toutes les optimisations terminées ===", "Système");
            }
            finally
            {
                _suppressPerItemConfirmation = false;
                IsRunAllInProgress = false;

                _runAllCts.Dispose();
                _runAllCts = null;

                IsBusy = false;
                StatusMessage = "Prêt";
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private void StopRunAllOptimizations(object? _)
        {
            if (_runAllCts == null || _runAllCts.IsCancellationRequested) return;

            _runAllCts.Cancel();
            StatusMessage = "Arrêt demandé…";
            Log.Warn("Arrêt demandé. L'optimisation en cours va se terminer avant interruption.", "Système");
            CommandManager.InvalidateRequerySuggested();
        }

        private async Task CreateRestorePointAsync(object? _)
        {
            IsBusy        = true;
            StatusMessage = "Création du point de restauration…";
            Log.Info("Création d'un point de restauration Windows…", "Système");
            
            var result = await SystemService.CreateRestorePointAsync();
            
            if (result.Success)
                Log.Success(result.DisplayMessage, "Système");
            else
                Log.Error(result.DisplayMessage, "Système");
            
            TerminalOutput = Log.TerminalText;
            IsBusy         = false;
            StatusMessage  = "Prêt";
        }

        private void ExportLogs(object? _)
        {
            string path = Log.Export();
            if (path.StartsWith("❌"))
                MessageBox.Show(path, "Erreur export", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                MessageBox.Show($"Logs exportés :\n{path}", "Export réussi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ─────────────────────── REFRESH ───────────────────────────────────────
        private void RefreshSystemInfo()
        {
            var info = SystemService.GetSystemInfo();
            Application.Current?.Dispatcher.Invoke(() =>
            {
                SystemInfo.CpuName     = info.CpuName;
                SystemInfo.OsName      = info.OsName;
                SystemInfo.MachineName = info.MachineName;
                SystemInfo.Uptime      = info.Uptime;
                SystemInfo.RamUsedGb   = info.RamUsedGb;
                SystemInfo.RamTotalGb  = info.RamTotalGb;
                SystemInfo.RamPercent  = info.RamPercent;
                SystemInfo.DiskFreeGb  = info.DiskFreeGb;
                SystemInfo.DiskTotalGb = info.DiskTotalGb;
            });
        }

        /// <summary>
        /// Refresh périodique (timer 2s) : CPU + RAM + disque C: + uptime.
        /// Les infos statiques (CPU name, OS, machine) sont initialisées une fois via RefreshSystemInfo().
        /// </summary>
        private void RefreshRealtimeStats()
        {
            try
            {
                float cpu = SystemService.GetCpuUsage();
                var stats = SystemService.GetRealtimeStats();

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    CpuUsage              = cpu;
                    SystemInfo.CpuUsage   = cpu;
                    SystemInfo.RamUsedGb  = stats.RamUsedGb;
                    SystemInfo.RamTotalGb = stats.RamTotalGb;
                    SystemInfo.RamPercent = stats.RamPercent;
                    SystemInfo.DiskFreeGb = stats.DiskFreeGb;
                    SystemInfo.DiskTotalGb = stats.DiskTotalGb;
                    SystemInfo.Uptime     = stats.Uptime;
                });
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Refresh stats échoué : {ex.Message}", "MainViewModel");
            }
        }

        // ─────────────────────── DISPOSE (FIX P1-05) ───────────────────────────
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            
            if (disposing)
            {
                if (_runAllCts != null)
                {
                    _runAllCts.Cancel();
                    _runAllCts.Dispose();
                    _runAllCts = null;
                }

                // Arrêter et disposer le timer
                _refreshTimer.Stop();
                _refreshTimer.Dispose();
                
                // Désabonner des events
                if (_logPropertyChangedHandler != null)
                {
                    Log.PropertyChanged -= _logPropertyChangedHandler;
                    _logPropertyChangedHandler = null;
                }
                
                // Cleanup des ressources système
                SystemService.Cleanup();
            }
            
            _disposed = true;
        }

        ~MainViewModel()
        {
            Dispose(false);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

        private static bool IsCriticalOptimization(string id)
            => CriticalOptimizationIds.Contains(id);

        private static string NormalizeStartupPage(string? page)
        {
            return page switch
            {
                "Dashboard" => "Dashboard",
                "Gaming" => "Gaming",
                "OldPC" => "OldPC",
                "Cleaning" => "Cleaning",
                "Network" => "Network",
                "Terminal" => "Terminal",
                _ => "Dashboard"
            };
        }

        private static string BuildCriticalSimulationMessage(OptimizationItem item) => item.Id switch
        {
            "net_reset" => "Simulation uniquement : reset Winsock/IP non appliqué.",
            "net_reset_firewall" => "Simulation uniquement : reset pare-feu non appliqué.",
            "net_telemetry_block" => "Simulation uniquement : fichier hosts inchangé.",
            _ => $"Simulation uniquement : {item.Name} non exécuté."
        };

        private static int GetRunAllPriority(OptimizationItem item)
        {
            int purposePriority = item.Purpose switch
            {
                OptimizationPurpose.Performance => 10,
                OptimizationPurpose.Maintenance => 30,
                OptimizationPurpose.Troubleshooting => 45,
                OptimizationPurpose.SecurityPrivacy => 55,
                _ => 40
            };

            int confidencePenalty = (100 - item.ConfidenceScore) / 5;
            int advancedPenalty = item.IsAdvanced ? 20 : 0;
            int adminPenalty = item.RequiresAdmin ? 4 : 0;
            int rebootPenalty = item.RequiresReboot ? 4 : 0;

            return purposePriority + confidencePenalty + advancedPenalty + adminPenalty + rebootPenalty;
        }
    }
}
