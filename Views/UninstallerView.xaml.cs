using System.Collections.Generic;
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
    /// v2.3.0 — Désinstalleur intégré (UserControl). Chargement lazy à la 1re visibilité.
    /// </summary>
    public partial class UninstallerView : UserControl
    {
        private readonly ObservableCollection<InstalledProgram> _all = new();
        private readonly ObservableCollection<InstalledProgram> _filtered = new();
        private bool _loadedOnce;

        public UninstallerView()
        {
            InitializeComponent();
            ProgramsList.ItemsSource = _filtered;
            IsVisibleChanged += async (_, e) =>
            {
                if (IsVisible && !_loadedOnce) { _loadedOnce = true; await LoadProgramsAsync(); }
            };
        }

        private async Task LoadProgramsAsync()
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            var programs = await Task.Run(() => UninstallerService.GetInstalledPrograms());
            _all.Clear();
            foreach (var p in programs) _all.Add(p);
            ApplyFilter(SearchBox.Text);
            CountText.Text = string.Format(Strings.Uninstaller_Count, _all.Count);
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void ApplyFilter(string query)
        {
            _filtered.Clear();
            IEnumerable<InstalledProgram> src = _all;
            if (!string.IsNullOrWhiteSpace(query))
                src = _all.Where(p =>
                    p.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                    p.Publisher.Contains(query, System.StringComparison.OrdinalIgnoreCase));
            foreach (var p in src) _filtered.Add(p);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text) ? Visibility.Visible : Visibility.Collapsed;
            ApplyFilter(SearchBox.Text);
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) => await LoadProgramsAsync();

        private async void Uninstall_Click(object sender, RoutedEventArgs e)
        {
            var selected = _all.Where(p => p.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(Strings.Uninstaller_NoSelection, Strings.Uninstaller_Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                string.Format(Strings.Uninstaller_ConfirmUninstall, selected.Count),
                Strings.Uninstaller_Title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            UninstallButton.IsEnabled = false;

            foreach (var prog in selected)
            {
                StatusText.Text = string.Format(Strings.Uninstaller_Uninstalling, prog.Name);
                bool ok = await UninstallerService.UninstallAsync(prog);
                if (!ok) continue;

                var leftovers = await Task.Run(() => UninstallerService.ScanLeftovers(prog));
                if (leftovers.Count > 0)
                {
                    string preview = string.Join("\n", leftovers.Take(10).Select(l => "  • " + l));
                    if (leftovers.Count > 10) preview += $"\n  … +{leftovers.Count - 10}";

                    var del = MessageBox.Show(
                        string.Format(Strings.Uninstaller_LeftoversFound, prog.Name, leftovers.Count, preview),
                        Strings.Uninstaller_LeftoversTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);

                    if (del == MessageBoxResult.Yes)
                    {
                        var (removed, failed) = await Task.Run(() => UninstallerService.DeleteLeftovers(leftovers));
                        LogService.Instance.Success(
                            string.Format(Strings.Uninstaller_LeftoversRemoved, prog.Name, removed, failed), "Système");
                    }
                }
            }

            StatusText.Text = "";
            UninstallButton.IsEnabled = true;
            await LoadProgramsAsync();
        }
    }
}
