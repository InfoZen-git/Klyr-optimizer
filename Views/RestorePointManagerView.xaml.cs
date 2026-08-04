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
    /// v2.4.0 — Gestionnaire de points de restauration intégré.
    /// Liste + création + ouverture de l'assistant Windows + suppression (espace).
    /// </summary>
    public partial class RestorePointManagerView : UserControl
    {
        private readonly ObservableCollection<RestorePointEntry> _points = new();
        private bool _loadedOnce;

        public RestorePointManagerView()
        {
            InitializeComponent();
            PointsList.ItemsSource = _points;
            IsVisibleChanged += async (_, e) =>
            {
                if (IsVisible && !_loadedOnce) { _loadedOnce = true; await LoadAsync(); }
            };
        }

        private async Task LoadAsync()
        {
            StatusText.Text = Strings.Restore_Loading;
            var points = await Task.Run(() => RestorePointService.GetRestorePoints());
            _points.Clear();
            foreach (var p in points) _points.Add(p);
            EmptyText.Visibility = _points.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = string.Format(Strings.Restore_Status, _points.Count);
        }

        private async void Create_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireAdmin()) return;

            BtnCreate.IsEnabled = false;
            StatusText.Text = Strings.Restore_Creating;
            bool ok = await RestorePointService.CreateAsync(Strings.Restore_ManualPointName);
            MessageBox.Show(ok ? Strings.Restore_Created : Strings.Restore_CreateFailed,
                Strings.Restore_Title, MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            BtnCreate.IsEnabled = true;
            await LoadAsync();
        }

        private void Open_Click(object sender, RoutedEventArgs e)
        {
            if (!RestorePointService.OpenSystemRestore())
                MessageBox.Show(Strings.Restore_OpenFailed, Strings.Restore_Title,
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void DeleteAll_Click(object sender, RoutedEventArgs e)
        {
            if (!RequireAdmin()) return;

            var confirm = MessageBox.Show(Strings.Restore_DeleteConfirm, Strings.Restore_Title,
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            BtnDelete.IsEnabled = false;
            StatusText.Text = Strings.Restore_Deleting;
            bool ok = await RestorePointService.DeleteAllAsync();
            MessageBox.Show(ok ? Strings.Restore_Deleted : Strings.Restore_DeleteFailed,
                Strings.Restore_Title, MessageBoxButton.OK,
                ok ? MessageBoxImage.Information : MessageBoxImage.Warning);
            BtnDelete.IsEnabled = true;
            await LoadAsync();
        }

        private bool RequireAdmin()
        {
            if (AdminChecker.IsRunningAsAdmin()) return true;
            MessageBox.Show(Strings.Restore_AdminRequired, Strings.Restore_Title,
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
