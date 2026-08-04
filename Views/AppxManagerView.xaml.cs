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
    /// v2.5.0 — Débloat UWP avancé. Liste les apps du Store désinstallables, avec cases à
    /// cocher et désinstallation par lot. Chargement lazy à la 1re visibilité.
    /// </summary>
    public partial class AppxManagerView : UserControl
    {
        private readonly ObservableCollection<AppxEntry> _apps = new();
        private bool _loadedOnce;

        public AppxManagerView()
        {
            InitializeComponent();
            AppList.ItemsSource = _apps;
            IsVisibleChanged += async (_, e) =>
            {
                if (IsVisible && !_loadedOnce) { _loadedOnce = true; await LoadAsync(); }
            };
        }

        private async Task LoadAsync()
        {
            OverlayText.Text = Strings.Appx_Loading;
            OverlayPanel.Visibility = Visibility.Visible;
            var apps = await AppxService.GetInstalledAsync();
            _apps.Clear();
            foreach (var a in apps) _apps.Add(a);
            StatusText.Text = string.Format(Strings.Appx_Status, _apps.Count);
            OverlayPanel.Visibility = Visibility.Collapsed;
        }

        private async void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            var selected = _apps.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(Strings.Appx_NoneSelected, Strings.Appx_Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                string.Format(Strings.Appx_ConfirmUninstall, selected.Count),
                Strings.Appx_Title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            BtnUninstall.IsEnabled = false;
            OverlayText.Text = Strings.Appx_Uninstalling;
            OverlayPanel.Visibility = Visibility.Visible;

            int ok = 0, fail = 0;
            foreach (var app in selected)
            {
                bool success = await AppxService.UninstallAsync(app);
                if (success) { _apps.Remove(app); ok++; }
                else fail++;
            }

            OverlayPanel.Visibility = Visibility.Collapsed;
            BtnUninstall.IsEnabled = true;
            StatusText.Text = string.Format(Strings.Appx_Status, _apps.Count);
            LogService.Instance.Success(string.Format(Strings.Appx_Done, ok, fail), "Système");
            MessageBox.Show(string.Format(Strings.Appx_Done, ok, fail), Strings.Appx_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
