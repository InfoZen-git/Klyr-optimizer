using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Klyr.Models;
using Klyr.Resources;
using Klyr.Services;

namespace Klyr.Views
{
    /// <summary>
    /// v2.3.0 — Software Updater intégré (winget). Chargement lazy à la 1re visibilité.
    /// </summary>
    public partial class UpdaterView : UserControl
    {
        private readonly ObservableCollection<UpgradablePackage> _packages = new();
        private bool _loadedOnce;

        public UpdaterView()
        {
            InitializeComponent();
            PackagesList.ItemsSource = _packages;
            IsVisibleChanged += async (_, e) =>
            {
                if (IsVisible && !_loadedOnce) { _loadedOnce = true; await LoadAsync(); }
            };
        }

        private async Task LoadAsync()
        {
            ShowOverlay(Strings.Updater_Loading, "");
            UpdateButton.IsEnabled = false;

            if (!await WingetService.IsAvailableAsync())
            {
                ShowOverlay(Strings.Updater_WingetMissing, "");
                return;
            }

            var pkgs = await WingetService.GetUpgradableAsync();
            _packages.Clear();
            foreach (var p in pkgs) _packages.Add(p);

            if (_packages.Count == 0)
            {
                ShowOverlay(Strings.Updater_AllUpToDate, "");
            }
            else
            {
                OverlayPanel.Visibility = Visibility.Collapsed;
                StatusText.Text = string.Format(Strings.Updater_Count, _packages.Count);
                UpdateButton.IsEnabled = true;
            }
        }

        private async void Update_Click(object sender, RoutedEventArgs e)
        {
            var selected = _packages.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(Strings.Updater_NoSelection, Strings.Updater_Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            UpdateButton.IsEnabled = false;
            int ok = 0, fail = 0;
            foreach (var pkg in selected)
            {
                StatusText.Text = string.Format(Strings.Updater_Updating, pkg.Name);
                bool success = await WingetService.UpgradePackageAsync(pkg.Id);
                if (success) ok++; else fail++;
            }

            LogService.Instance.Success(string.Format(Strings.Updater_Done, ok, fail), "Système");
            StatusText.Text = string.Format(Strings.Updater_Done, ok, fail);
            await LoadAsync();
        }

        private void ShowOverlay(string text, string glyph)
        {
            OverlayPanel.Visibility = Visibility.Visible;
            OverlayText.Text = text;
            OverlayIcon.Text = glyph;
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadAsync();

        /// <summary>
        /// winget ne gère pas les mises à jour de Windows : on ouvre les paramètres Windows Update.
        /// </summary>
        private void WindowsUpdate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "ms-settings:windowsupdate",
                    UseShellExecute = true
                });
            }
            catch (System.Exception ex)
            {
                LogService.Instance.Warn($"Ouverture de Windows Update impossible : {ex.Message}", "Système");
            }
        }
    }
}
