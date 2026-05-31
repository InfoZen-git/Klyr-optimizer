using System.Windows;
using System.Windows.Input;
using Klyr.Services;
using Klyr.ViewModels;

namespace Klyr.Views
{
    public partial class SettingsWindow : Window
    {
        public SettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
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
                MessageBox.Show(
                    Klyr.Resources.Strings.Settings_LanguageRestartHint,
                    Klyr.Resources.Strings.Settings_Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }

            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
        private void Close_Click(object sender, RoutedEventArgs e)   => Close();
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }
    }
}
