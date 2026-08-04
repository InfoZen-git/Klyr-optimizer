using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Klyr.Models;
using Klyr.Resources;
using Klyr.Services;

namespace Klyr.Views
{
    /// <summary>
    /// v2.5.0 — Gros fichiers &amp; doublons. Choix d'un dossier, scan (gros fichiers ou
    /// doublons), puis suppression vers la Corbeille. Le scan tourne en tâche de fond,
    /// annulable, et survit au changement d'onglet (pas de rechargement auto).
    /// </summary>
    public partial class FileFinderView : UserControl
    {
        private const int  TopN         = 200;
        private const long DupMinBytes  = 1024 * 1024; // 1 Mo

        private readonly ObservableCollection<FileEntry> _results = new();
        private CancellationTokenSource? _cts;
        private string _folder = "";

        public FileFinderView()
        {
            InitializeComponent();
            ResultsList.ItemsSource = _results;
        }

        private void ChooseFolder_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = Strings.Files_ChooseFolder,
                InitialDirectory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile)
            };
            if (dlg.ShowDialog() == true)
            {
                _folder = dlg.FolderName;
                FolderText.Text = _folder;
            }
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_folder))
            {
                MessageBox.Show(Strings.Files_NoFolder, Strings.Files_Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _cts = new CancellationTokenSource();
            bool duplicates = ModeDup.IsChecked == true;

            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            EmptyText.Visibility = Visibility.Collapsed;
            _results.Clear();
            StatusText.Text = Strings.Files_Scanning;

            try
            {
                var token = _cts.Token;
                string folder = _folder;
                var found = await Task.Run(() => duplicates
                    ? FileFinderService.FindDuplicates(folder, DupMinBytes, token)
                    : FileFinderService.FindLargeFiles(folder, TopN, token), token);

                foreach (var f in found) _results.Add(f);
                EmptyText.Visibility = _results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                StatusText.Text = string.Format(Strings.Files_Status, _results.Count);
            }
            catch (OperationCanceledException)
            {
                StatusText.Text = string.Format(Strings.Files_Status, _results.Count);
            }
            catch (System.Exception ex)
            {
                StatusText.Text = ex.Message;
            }
            finally
            {
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            try { _cts?.Cancel(); } catch { /* déjà disposé */ }
        }

        private void Delete_Click(object sender, RoutedEventArgs e)
        {
            var selected = _results.Where(f => f.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show(Strings.Files_NoneSelected, Strings.Files_Title,
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(
                string.Format(Strings.Files_ConfirmDelete, selected.Count),
                Strings.Files_Title, MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            int ok = 0;
            foreach (var f in selected)
            {
                if (SafeDeleteService.ToRecycleBin(f.Path)) { _results.Remove(f); ok++; }
            }

            StatusText.Text = string.Format(Strings.Files_Status, _results.Count);
            LogService.Instance.Success(string.Format(Strings.Files_Done, ok), "Système");
            MessageBox.Show(string.Format(Strings.Files_Done, ok), Strings.Files_Title,
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
