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
    /// v2.4.0 — Gestionnaire de services Windows intégré. Chargement lazy à la 1re visibilité.
    /// Lecture sans admin ; le toggle exige l'élévation (sinon message).
    /// </summary>
    public partial class ServicesManagerView : UserControl
    {
        private readonly ObservableCollection<ServiceEntry> _entries = new();
        private bool _loadedOnce;

        public ServicesManagerView()
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
            var entries = await Task.Run(() => ServicesManagerService.GetServices());
            _entries.Clear();
            foreach (var en in entries) _entries.Add(en);
            StatusText.Text = string.Format(Strings.Services_Status, _entries.Count);
            OverlayPanel.Visibility = Visibility.Collapsed;
        }

        private async void Toggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not CheckBox cb || cb.Tag is not ServiceEntry entry) return;

            // Le changement de service nécessite l'élévation.
            if (!AdminChecker.IsRunningAsAdmin())
            {
                cb.IsChecked = entry.IsEnabled; // annule le clic visuel
                MessageBox.Show(Strings.Services_AdminRequired, Strings.Services_Title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool target = !entry.IsEnabled;
            cb.IsEnabled = false;
            StatusText.Text = Strings.Services_Applying;

            bool ok = await ServicesManagerService.SetEnabledAsync(entry, target);

            if (!ok)
                MessageBox.Show(string.Format(Strings.Services_ToggleFailed, entry.DisplayName),
                    Strings.Services_Title, MessageBoxButton.OK, MessageBoxImage.Warning);

            cb.IsChecked = entry.IsEnabled;
            cb.IsEnabled = true;
            StatusText.Text = string.Format(Strings.Services_Status, _entries.Count);
        }
    }
}
