using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Klyr.Models;
using Klyr.Services;

namespace Klyr.Views
{
    /// <summary>
    /// v2.5.0 — Rapport de santé actionnable. Calcule les recommandations à la 1re visibilité
    /// (et sur clic « Actualiser »). Le DataContext (MainViewModel) est hérité pour permettre
    /// aux boutons « Corriger » de naviguer vers le bon outil/module.
    /// </summary>
    public partial class HealthReportView : UserControl
    {
        private readonly ObservableCollection<HealthCheck> _checks = new();
        private bool _loadedOnce;

        public HealthReportView()
        {
            InitializeComponent();
            ChecksList.ItemsSource = _checks;
            IsVisibleChanged += async (_, e) =>
            {
                if (IsVisible && !_loadedOnce) { _loadedOnce = true; await LoadAsync(); }
            };
        }

        private async Task LoadAsync()
        {
            OverlayPanel.Visibility = Visibility.Visible;
            var checks = await Task.Run(() => HealthReportService.Run());
            _checks.Clear();
            foreach (var c in checks) _checks.Add(c);
            OverlayPanel.Visibility = Visibility.Collapsed;
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();
    }
}
