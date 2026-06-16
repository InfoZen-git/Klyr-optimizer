using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Klyr.Commands;
using Klyr.Models;
using Klyr.Resources;
using Klyr.Services;

namespace Klyr.ViewModels
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
            "Dashboard" => Strings.PageTitle_Dashboard,
            "Gaming"    => Strings.PageTitle_Gaming,
            "OldPC"     => Strings.PageTitle_OldPC,
            "Cleaning"  => Strings.PageTitle_Cleaning,
            "Network"   => Strings.PageTitle_Network,
            "Streaming" => Strings.PageTitle_Streaming,
            "Terminal"  => Strings.PageTitle_Terminal,
            "Legal"     => Strings.PageTitle_Legal,
            "Uninstaller"  => Strings.Uninstaller_Title,
            "Updater"      => Strings.Updater_Title,
            "DiskAnalyzer" => Strings.DiskAnalyzer_Title,
            "Startup"      => Strings.Startup_Title,
            "Browser"      => Strings.Browser_Title,
            _           => CurrentPage
        };

        // ─────────────────────── OPTIMISATIONS ─────────────────────────────────
        public ObservableCollection<OptimizationItem> CurrentOptimizations { get; } = new();

        /// <summary>v2.3.0 — En-tête module localisé : « {n} optimisations disponibles ».</summary>
        public string CurrentOptimizationsCountText =>
            string.Format(Strings.Module_OptimizationsAvailable, CurrentOptimizations.Count);

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

        private string _statusMessage = Strings.Status_Ready;
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

        // ─────────────────────── MISE À JOUR (v2.3.0) ──────────────────────────
        private bool _updateAvailable;
        public  bool UpdateAvailable
        {
            get => _updateAvailable;
            set { _updateAvailable = value; OnPropertyChanged(); }
        }

        private string _updateLabel = "";
        public  string UpdateLabel
        {
            get => _updateLabel;
            set { _updateLabel = value; OnPropertyChanged(); }
        }

        private string _updateUrl = "";

        // ─────────────────────── COMMANDS ──────────────────────────────────────
        public AsyncRelayCommand RunOptimizationCommand     { get; }
        public AsyncRelayCommand RunAllOptimizationsCommand { get; }
        public AsyncRelayCommand CreateRestorePointCommand  { get; }
        public RelayCommand      StopRunAllCommand          { get; }
        public RelayCommand      ExportLogsCommand          { get; }
        public RelayCommand      ClearLogsCommand           { get; }
        public RelayCommand      NavigateCommand            { get; }
        public RelayCommand      OpenToolCommand            { get; }   // v2.3.0
        public RelayCommand      OpenUpdateCommand          { get; }   // v2.3.0

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
            OpenToolCommand            = new RelayCommand(OpenTool);   // v2.3.0
            OpenUpdateCommand          = new RelayCommand(_ => OpenUpdatePage());   // v2.3.0

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

            Log.Info(string.Format(Strings.Log_AppStarted, "v" + DiagnosticService.AppVersion), "Système");
            Log.Info(Strings.Log_PrivacyNotice, "RGPD");

            // v2.3.0 — Vérification de mise à jour en arrière-plan (best-effort, non bloquant)
            _ = CheckForUpdateAsync();
        }

        // ─────────────────────── MISE À JOUR (v2.3.0) ──────────────────────────
        private async Task CheckForUpdateAsync()
        {
            try
            {
                var info = await UpdateService.CheckForUpdateAsync();
                if (info == null) return;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    _updateUrl = info.Url;
                    UpdateLabel = string.Format(Strings.Update_Available, info.Version);
                    UpdateAvailable = true;
                });
                Log.Info(string.Format(Strings.Log_UpdateFound, info.Version), "Système");
            }
            catch { /* best-effort */ }
        }

        private void OpenUpdatePage()
        {
            if (string.IsNullOrWhiteSpace(_updateUrl)) return;

            // SÉCURITÉ (défense en profondeur) : n'ouvrir que des URL http(s).
            // L'URL provient de l'API GitHub ; on refuse tout autre schéma
            // (file:, javascript:, chemin local…) avant de la passer au shell.
            if (!Uri.TryCreate(_updateUrl, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            {
                Log.Warn("URL de mise à jour ignorée (schéma non autorisé).", "Système");
                return;
            }

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = uri.AbsoluteUri,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                Log.Error($"Impossible d'ouvrir la page de mise à jour : {ex.Message}", "Système");
            }
        }

        // ─────────────────────── NAVIGATION ────────────────────────────────────
        // FIX: cache des items par page pour préserver l'état (IsRunning, Progress, Status)
        // quand l'utilisateur navigue entre pages pendant qu'une optimisation tourne.
        private readonly Dictionary<string, List<OptimizationItem>> _pageOptimizationsCache = new();

        // ─── Compteurs dynamiques par catégorie (sidebar) ───
        public int GamingCount    => GetModuleCount("Gaming");
        public int OldPCCount     => GetModuleCount("OldPC");
        public int CleaningCount  => GetModuleCount("Cleaning");
        public int NetworkCount   => GetModuleCount("Network");
        public int StreamingCount => GetModuleCount("Streaming");

        // v2.2.0 — Versions formatées localisées pour le sidebar ("{0} optimisations" / "{0} optimizations")
        public string GamingCountText    => string.Format(Strings.Nav_OptimizationsCount, GamingCount);
        public string OldPCCountText     => string.Format(Strings.Nav_OptimizationsCount, OldPCCount);
        public string CleaningCountText  => string.Format(Strings.Nav_OptimizationsCount, CleaningCount);
        public string NetworkCountText   => string.Format(Strings.Nav_OptimizationsCount, NetworkCount);
        public string StreamingCountText => string.Format(Strings.Nav_OptimizationsCount, StreamingCount);

        private int GetModuleCount(string module)
        {
            if (!_pageOptimizationsCache.TryGetValue(module, out var items))
            {
                items = module switch
                {
                    "Gaming"    => GamingOptimizations.GetOptimizations(),
                    "OldPC"     => OldPcOptimizations.GetOptimizations(),
                    "Cleaning"  => CleaningOptimizations.GetOptimizations(),
                    "Network"   => NetworkOptimizations.GetOptimizations(),
                    "Streaming" => StreamingOptimizations.GetOptimizations(),
                    _           => new List<OptimizationItem>()
                };
                _pageOptimizationsCache[module] = items;
            }
            return SettingsService.Current.ShowAdvancedOptimizations
                ? items.Count
                : items.Count(i => !i.IsAdvanced);
        }

        private void NotifyAllCountsChanged()
        {
            OnPropertyChanged(nameof(GamingCount));
            OnPropertyChanged(nameof(OldPCCount));
            OnPropertyChanged(nameof(CleaningCount));
            OnPropertyChanged(nameof(NetworkCount));
            OnPropertyChanged(nameof(StreamingCount));
            OnPropertyChanged(nameof(GamingCountText));
            OnPropertyChanged(nameof(OldPCCountText));
            OnPropertyChanged(nameof(CleaningCountText));
            OnPropertyChanged(nameof(NetworkCountText));
            OnPropertyChanged(nameof(StreamingCountText));
        }

        private void LoadPageOptimizations()
        {
            if (!_pageOptimizationsCache.TryGetValue(CurrentPage, out var items))
            {
                items = CurrentPage switch
                {
                    "Gaming"    => GamingOptimizations.GetOptimizations(),
                    "OldPC"     => OldPcOptimizations.GetOptimizations(),
                    "Cleaning"  => CleaningOptimizations.GetOptimizations(),
                    "Network"   => NetworkOptimizations.GetOptimizations(),
                    "Streaming" => StreamingOptimizations.GetOptimizations(),
                    _           => new List<OptimizationItem>()
                };
                _pageOptimizationsCache[CurrentPage] = items;
            }

            var filtered = SettingsService.Current.ShowAdvancedOptimizations
                ? items
                : items.Where(i => !i.IsAdvanced).ToList();

            CurrentOptimizations.Clear();
            foreach (var item in filtered) CurrentOptimizations.Add(item);

            OnPropertyChanged(nameof(CurrentOptimizationsCountText));
        }

        /// <summary>
        /// Recharge la page courante ET notifie les 4 compteurs sidebar
        /// (appelé après changement de settings, notamment ShowAdvancedOptimizations).
        /// </summary>
        public void ReloadCurrentOptimizations()
        {
            LoadPageOptimizations();
            NotifyAllCountsChanged();
        }

        // ─────────────────────── RUN OPTIMISATION ──────────────────────────────
        private async Task RunOptimizationAsync(object? param)
        {
            var item = param as OptimizationItem ?? null;
            if (item?.Action == null) return;

            if (_runAllCts?.IsCancellationRequested == true)
            {
                Log.Warn($"{item.Name} ignoré (arrêt global demandé).", item.Category);
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
                    string.Format(Strings.Dialog_Critical_Message, item.Name),
                    Strings.Dialog_Critical_Title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (criticalConfirm != MessageBoxResult.Yes)
                {
                    Log.Warn($"{item.Name} annulé (action critique non confirmée).", item.Category);
                    return;
                }
            }

            // FIX P0-02: Garde admin stricte - bloquer si RequiresAdmin et non admin
            if (item.RequiresAdmin && !AdminChecker.IsRunningAsAdmin())
            {
                var result = MessageBox.Show(
                    string.Format(Strings.Dialog_AdminRequired_Message, item.Name),
                    Strings.Dialog_AdminRequired_Title,
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = Environment.ProcessPath ?? "Klyr.exe",
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
                    Log.Warn($"{item.Name} ignoré (droits admin requis).", item.Category);
                }
                return;
            }

            // Confirmation avant exécution
            if (SettingsService.Current.ConfirmBeforeRun && !_suppressPerItemConfirmation)
            {
                var confirm = MessageBox.Show(
                    string.Format(Strings.Dialog_RunOpt_Message, item.Name, item.Description),
                    Strings.Dialog_Confirm_Title,
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Question);
                if (confirm != MessageBoxResult.OK) return;
            }

            // Point de restauration automatique avant les actions admin
            if (SettingsService.Current.AutoRestorePoint && item.RequiresAdmin)
            {
                Log.Info("Création d'un point de restauration automatique…", "Système");
                StatusMessage = Strings.Status_CreatingRestorePoint;
                var restoreResult = await SystemService.CreateRestorePointAsync($"Klyr – avant : {item.Name}");
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
            StatusMessage  = string.Format(Strings.Status_RunningItem, item.Name);
            Log.Info($"Démarrage : {item.Name}", item.Category);

            OptimizationBenchmarkSnapshot? benchmarkBefore = null;
            bool benchmarkEnabled = SettingsService.Current.EnableOptimizationBenchmarks && item.IsBenchmarkCandidate;
            if (benchmarkEnabled)
            {
                try
                {
                    benchmarkBefore = await OptimizationBenchmarkService.CaptureAsync(item);
                    Log.Info($"Baseline capturée pour {item.Name}.", "Benchmark");
                }
                catch (Exception ex)
                {
                    benchmarkEnabled = false;
                    Log.Warn($"Benchmark baseline ignoré ({item.Name}) : {ex.Message}", "Benchmark");
                }
            }

            var benchmarkWatch = System.Diagnostics.Stopwatch.StartNew();

            // v2.3.0 — Mesure de l'espace libre AVANT une optim de nettoyage.
            // Le delta après exécution (s'il est positif) est journalisé dans les logs.
            long freeBefore = item.IsDiskCleanup ? GetSystemDriveFreeBytes() : -1;

            // v2.2.0 — CTS par item, lié au _runAllCts si une exécution groupée est en cours.
            // Le bouton cancel de l'item annule perItemCts → kill du process via SystemService.
            CancellationTokenSource perItemCts;
            if (_runAllCts != null)
                perItemCts = CancellationTokenSource.CreateLinkedTokenSource(_runAllCts.Token);
            else
                perItemCts = new CancellationTokenSource();
            item.CancellationTokenSource = perItemCts;

            try
            {
                // Lance l'action AVEC progression animée + cancellation propagée
                string result = await ProgressHelper.RunWithProgressAsync(
                    item,
                    item.Action,
                    perItemCts.Token,
                    estimatedMs: EstimateMs(item.Id)
                );

                item.Progress = 100;
                bool isCancelled = perItemCts.IsCancellationRequested;
                bool isError = !isCancelled && IsErrorResult(result);
                item.Status = isCancelled ? "Annulé" : (isError ? "Erreur" : "Terminé");

                if (isCancelled)
                    Log.Warn($"{item.Name} : annulé par l'utilisateur.", item.Category);
                else if (isError)
                    Log.Error(result, item.Category);
                else
                    Log.Success(result, item.Category);

                // v2.3.0 — On journalise l'espace réellement libéré sur C: (delta avant/après)
                // lorsqu'une optim de nettoyage réussit. Les logs tiennent lieu d'historique.
                // Seuil de 1 Mo pour ignorer le bruit (écritures système en arrière-plan).
                if (item.IsDiskCleanup && !isCancelled && !isError && freeBefore >= 0)
                {
                    long freed = GetSystemDriveFreeBytes() - freeBefore;
                    if (freed > 1_048_576)
                        Log.Info(string.Format(Strings.Log_SpaceFreed, item.Name, FormatFreed(freed)), item.Category);
                }

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
                        string.Format(Strings.Dialog_Reboot_Message, item.Name),
                        Strings.Dialog_Reboot_Title,
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (OperationCanceledException)
            {
                benchmarkWatch.Stop();
                item.Status = "Annulé";
                item.Progress = 0;
                Log.Warn($"{item.Name} : annulé par l'utilisateur.", item.Category);
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
                // v2.2.0 — Cleanup CTS de l'item
                item.CancellationTokenSource = null;
                try { perItemCts.Dispose(); } catch { /* déjà disposed */ }

                if (!IsRunAllInProgress)
                {
                    IsBusy = false;
                    StatusMessage = Strings.Status_Ready;
                }
            }
        }

        /// <summary>
        /// v2.3.0 — Espace libre du lecteur système (celui de Windows), en octets.
        /// Best-effort : retourne -1 si la lecture échoue (l'historique sera juste ignoré).
        /// </summary>
        private static long GetSystemDriveFreeBytes()
        {
            try
            {
                string root = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                return new DriveInfo(root).AvailableFreeSpace;
            }
            catch { return -1; }
        }

        /// <summary>Formate un nombre d'octets pour le log (Ko/Mo/Go).</summary>
        private static string FormatFreed(long b) => b switch
        {
            >= 1073741824 => $"{b / 1073741824.0:F2} Go",
            >= 1048576    => $"{b / 1048576.0:F1} Mo",
            >= 1024       => $"{b / 1024.0:F0} Ko",
            _             => $"{b} o"
        };

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
                    string.Format(Strings.Dialog_RunAll_Message, CurrentOptimizations.Count, PageTitle),
                    Strings.Dialog_RunAll_Title,
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
                        Log.Warn("Exécution groupée interrompue par l'utilisateur.", "Système");
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
                StatusMessage = Strings.Status_Ready;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// v2.3.0 — Navigue vers une vue outil intégrée (plus de fenêtre séparée).
        /// </summary>
        private void OpenTool(object? param)
        {
            string tool = param?.ToString() ?? "";
            if (!string.IsNullOrEmpty(tool))
                CurrentPage = tool;
        }

        private void StopRunAllOptimizations(object? _)
        {
            if (_runAllCts == null || _runAllCts.IsCancellationRequested) return;

            _runAllCts.Cancel();
            StatusMessage = Strings.Status_StopRequested;
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
            StatusMessage  = Strings.Status_Ready;
        }

        private void ExportLogs(object? _)
        {
            string path = Log.Export();
            if (IsErrorResult(path))
                MessageBox.Show(path, "Erreur export", MessageBoxButton.OK, MessageBoxImage.Error);
            else
                MessageBox.Show($"Logs exportés :\n{path}", "Export réussi", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Détecte si le résultat textuel d'une opération est une erreur.
        /// Remplace l'ancienne détection par préfixe emoji.
        /// </summary>
        private static bool IsErrorResult(string? result) =>
            !string.IsNullOrEmpty(result) &&
            (result.Contains("[ERR]")
             || result.StartsWith("Erreur",          StringComparison.OrdinalIgnoreCase)
             || result.StartsWith("Échec",           StringComparison.OrdinalIgnoreCase)
             || result.StartsWith("Échoué",          StringComparison.OrdinalIgnoreCase)
             || result.StartsWith("Test échoué",     StringComparison.OrdinalIgnoreCase)
             || result.StartsWith("Export échoué",   StringComparison.OrdinalIgnoreCase)
             || result.StartsWith("Élévation refusée", StringComparison.OrdinalIgnoreCase)
             || result.StartsWith("Impossible",      StringComparison.OrdinalIgnoreCase)
             || result.StartsWith("Droits insuffisants", StringComparison.OrdinalIgnoreCase));

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

                // v2.3.0 — Capteurs matériels (best-effort, ne throw jamais).
                // Sécurité : gated par EnableHardwareSensors (driver kernel WinRing0).
                var hw = SettingsService.Current.EnableHardwareSensors
                    ? HardwareMonitorService.Instance.Read()
                    : new HardwareSnapshot();

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

                    // v2.3.0 — capteurs HW
                    // Température CPU retirée de l'UI (driver ring0 bloqué par HVCI → lecture 0
                    // non fiable). On ne l'alimente plus. GPU/disque conservés.
                    // SystemInfo.CpuTempC  = hw.CpuTempC;
                    SystemInfo.GpuTempC     = hw.GpuTempC;
                    SystemInfo.GpuLoadPct   = hw.GpuLoadPct;
                    if (hw.GpuName != "—") SystemInfo.GpuName = hw.GpuName;
                    SystemInfo.DiskReadKBs  = hw.DiskReadKBs;
                    SystemInfo.DiskWriteKBs = hw.DiskWriteKBs;
                    SystemInfo.DiskTempC    = hw.DiskTempC;

                    // v2.3.0 — Performance Score recalculé à chaque tick
                    SystemInfo.PerformanceScore = PerformanceScoreService.Compute(SystemInfo);
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

                // v2.3.0 — Fermer les capteurs matériels (driver ring0)
                try { HardwareMonitorService.Instance.Dispose(); } catch { /* best-effort */ }
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
                "Gaming"    => "Gaming",
                "OldPC"     => "OldPC",
                "Cleaning"  => "Cleaning",
                "Network"   => "Network",
                "Streaming" => "Streaming",
                "Terminal"  => "Terminal",
                // v2.3.0 — pages outils intégrées
                "Uninstaller"  => "Uninstaller",
                "Updater"      => "Updater",
                "DiskAnalyzer" => "DiskAnalyzer",
                "Startup"      => "Startup",
                "Browser"      => "Browser",
                _           => "Dashboard"
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
