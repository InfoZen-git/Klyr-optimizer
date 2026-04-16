using System.Windows;
using System.Windows.Input;
using InfoZen.Services;
using InfoZen.ViewModels;

namespace InfoZen.Views
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
            ThemeService.ApplyTheme(s.Theme);
            SettingsService.Save();

            if (Application.Current.MainWindow?.DataContext is MainViewModel vm)
                vm.ReloadCurrentOptimizations();

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
