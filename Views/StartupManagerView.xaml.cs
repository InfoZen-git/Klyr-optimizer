using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Klyr.Models;
using Klyr.Resources;
using Klyr.Services;

namespace Klyr.Views
{
    /// <summary>
    /// v2.3.0 — Startup Manager intégré. Chargement lazy à la 1re visibilité.
    /// </summary>
    public partial class StartupManagerView : UserControl
    {
        private readonly ObservableCollection<StartupEntry> _entries = new();
        private bool _loadedOnce;

        public StartupManagerView()
        {
            InitializeComponent();
            EntriesList.ItemsSource = _entries;
            IsVisibleChanged += async (_, e) =>
            {
                if (IsVisible && !_loadedOnce) { _loadedOnce = true; await LoadAsync(); }
            };
        }

        private async Task LoadAsync()
        {
            OverlayPanel.Visibility = Visibility.Visible;
            var entries = await Task.Run(() => StartupManagerService.GetStartupEntries());
            _entries.Clear();
            foreach (var en in entries) _entries.Add(en);
            StatusText.Text = string.Format(Strings.Startup_Status, _entries.Count);
            OverlayPanel.Visibility = Visibility.Collapsed;
        }

        private void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not StartupEntry entry) return;
            bool target = !entry.IsEnabled;
            bool ok = StartupManagerService.SetEnabled(entry, target);
            if (!ok)
                MessageBox.Show(string.Format(Strings.Startup_ToggleFailed, entry.Name),
                    Strings.Startup_Title, MessageBoxButton.OK, MessageBoxImage.Warning);
            cb.IsChecked = entry.IsEnabled;
            StatusText.Text = string.Format(Strings.Startup_Status, _entries.Count);
        }

        /// <summary>v2.5.0 — Ajoute un exécutable choisi par l'utilisateur au démarrage (HKCU).</summary>
        private async void AddTask_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title  = Strings.Startup_AddTask,
                Filter = "Exécutables (*.exe)|*.exe|Tous les fichiers (*.*)|*.*",
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ProgramFiles)
            };
            if (dlg.ShowDialog() != true) return;

            bool ok = StartupManagerService.AddUserStartup(dlg.FileName);
            if (ok)
                await LoadAsync();
            else
                MessageBox.Show(Strings.Startup_AddFailed, Strings.Startup_Title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
