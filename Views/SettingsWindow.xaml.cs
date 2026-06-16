using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Klyr.Services;
using Klyr.ViewModels;

namespace Klyr.Views
{
    public partial class SettingsWindow : Window
    {
        private bool _scheduleLoading;

        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
            _ = LoadScheduleStateAsync();
        }

        private void LoadSettings()
        {
            var s = SettingsService.Current;
            ToggleConfirm.IsChecked    = s.ConfirmBeforeRun;
            ToggleAutoRestore.IsChecked = s.AutoRestorePoint;
            ToggleReboot.IsChecked     = s.ShowRebootWarning;
            ToggleSimulation.IsChecked = s.SimulationMode;
            ToggleAdvanced.IsChecked   = s.ShowAdvancedOptimizations;
            ToggleBenchmarks.IsChecked = s.EnableOptimizationBenchmarks;
            ToggleScroll.IsChecked     = s.AutoScrollTerminal;
            ToggleAdminBadge.IsChecked = s.ShowAdminBadge;
            ToggleHwSensors.IsChecked  = s.EnableHardwareSensors;
            ToggleUpdateCheck.IsChecked = s.EnableUpdateCheck;
            ToggleLightTheme.IsChecked = string.Equals(s.Theme, "Light", StringComparison.OrdinalIgnoreCase);

            // v2.2.0 — Langue
            switch ((s.Language ?? "auto").ToLowerInvariant())
            {
                case "fr": LangFr.IsChecked   = true; break;
                case "en": LangEn.IsChecked   = true; break;
                default:   LangAuto.IsChecked = true; break;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var s = SettingsService.Current;
            s.ConfirmBeforeRun  = ToggleConfirm.IsChecked    == true;
            s.AutoRestorePoint  = ToggleAutoRestore.IsChecked == true;
            s.ShowRebootWarning = ToggleReboot.IsChecked      == true;
            s.SimulationMode    = ToggleSimulation.IsChecked   == true;
            s.ShowAdvancedOptimizations = ToggleAdvanced.IsChecked == true;
            s.EnableOptimizationBenchmarks = ToggleBenchmarks.IsChecked == true;
            s.AutoScrollTerminal = ToggleScroll.IsChecked     == true;
            s.ShowAdminBadge    = ToggleAdminBadge.IsChecked  == true;
            s.EnableHardwareSensors = ToggleHwSensors.IsChecked == true;
            s.EnableUpdateCheck = ToggleUpdateCheck.IsChecked == true;
            s.Theme             = ToggleLightTheme.IsChecked  == true ? "Light" : "Dark";

            // v2.2.0 — Persistence langue (prend effet au prochain démarrage)
            string newLanguage =
                LangFr.IsChecked == true ? "fr" :
                LangEn.IsChecked == true ? "en" :
                "auto";
            bool languageChanged = !string.Equals(s.Language, newLanguage, StringComparison.OrdinalIgnoreCase);
            s.Language = newLanguage;

            ThemeService.ApplyTheme(s.Theme);
            SettingsService.Save();

            if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
                vm.ReloadCurrentOptimizations();

            if (languageChanged)
            {
                // v2.3.0 — La langue est appliquée via la culture au démarrage : on relance Klyr.
                MessageBox.Show(
                    Klyr.Resources.Strings.Settings_LanguageRestartNow,
                    Klyr.Resources.Strings.Settings_Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);

                RestartApplication();
                return;
            }

            Close();
        }

        /// <summary>
        /// v2.3.0 — Ferme Klyr et le relance (pour appliquer le changement de langue).
        /// Préserve le niveau d'élévation (asInvoker → hérite du token courant).
        /// </summary>
        private static void RestartApplication()
        {
            try
            {
                string? exe = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exe))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = exe,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"Redémarrage automatique échoué : {ex.Message}", "Système");
            }
            finally
            {
                Application.Current.Shutdown();
            }
        }

        // ── v2.3.0 — Scans programmés ──
        private async Task LoadScheduleStateAsync()
        {
            _scheduleLoading = true;
            try
            {
                bool enabled = await ScheduledScanService.IsEnabledAsync();
                ToggleSchedule.IsChecked = enabled;
                FreqPanel.IsEnabled = enabled;

                string freq = (SettingsService.Current.ScheduledScanFrequency ?? "Weekly");
                FreqDaily.IsChecked   = freq == "Daily";
                FreqWeekly.IsChecked  = freq == "Weekly";
                FreqMonthly.IsChecked = freq == "Monthly";
            }
            finally { _scheduleLoading = false; }
        }

        private ScanFrequency CurrentFrequency()
        {
            if (FreqDaily.IsChecked == true)   return ScanFrequency.Daily;
            if (FreqMonthly.IsChecked == true) return ScanFrequency.Monthly;
            return ScanFrequency.Weekly;
        }

        private async void ToggleSchedule_Click(object sender, RoutedEventArgs e)
        {
            if (_scheduleLoading) return;
            bool wantEnabled = ToggleSchedule.IsChecked == true;
            FreqPanel.IsEnabled = wantEnabled;

            bool ok = wantEnabled
                ? await ScheduledScanService.EnableAsync(CurrentFrequency())
                : await ScheduledScanService.DisableAsync();

            if (!ok)
            {
                MessageBox.Show(Klyr.Resources.Strings.Settings_ScheduleFailed,
                    Klyr.Resources.Strings.Settings_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                _scheduleLoading = true;
                ToggleSchedule.IsChecked = !wantEnabled;
                FreqPanel.IsEnabled = !wantEnabled;
                _scheduleLoading = false;
            }
            else if (wantEnabled)
            {
                SettingsService.Current.ScheduledScanFrequency = CurrentFrequency().ToString();
                SettingsService.Save();
            }
        }

        private async void Freq_Checked(object sender, RoutedEventArgs e)
        {
            if (_scheduleLoading) return;
            if (ToggleSchedule.IsChecked != true) return;
            // Re-crée la tâche avec la nouvelle fréquence
            bool ok = await ScheduledScanService.EnableAsync(CurrentFrequency());
            if (ok)
            {
                SettingsService.Current.ScheduledScanFrequency = CurrentFrequency().ToString();
                SettingsService.Save();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        private void Close_Click(object sender, RoutedEventArgs e)   => Close();
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
