using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Klyr.Models;
using Klyr.Resources;
using Klyr.Services;

namespace Klyr.Views
{
    /// <summary>
    /// v2.3.0 — Disk Analyzer intégré.
    /// Lancement/arrêt MANUELS. L'analyse continue si on change d'onglet (la vue reste vivante) ;
    /// elle n'est annulée que par le bouton Arrêter.
    /// </summary>
    public partial class DiskAnalyzerView : UserControl
    {
        private readonly ObservableCollection<DiskEntry> _entries = new();
        private string _currentPath = "";
        private CancellationTokenSource? _cts;
        private bool _isRunning;

        public DiskAnalyzerView()
        {
            InitializeComponent();
            EntriesList.ItemsSource = _entries;

            var drives = DiskAnalyzerService.GetDriveRoots();
            DriveCombo.ItemsSource = drives;
            if (drives.Count > 0)
            {
                DriveCombo.SelectedIndex = 0;
                _currentPath = drives[0];
                PathText.Text = _currentPath;
            }
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            string path = DriveCombo.SelectedItem as string ?? _currentPath;
            if (string.IsNullOrEmpty(path)) return;
            await AnalyzeAsync(path);
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            StatusText.Text = Strings.DiskAnalyzer_Stopped;
            SetRunning(false);
            if (_entries.Count == 0)
            {
                OverlayText.Text = Strings.DiskAnalyzer_Idle;
                OverlayPanel.Visibility = Visibility.Visible;
            }
        }

        private async Task AnalyzeAsync(string path)
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _currentPath = path;
            PathText.Text = path;
            OverlayText.Text = Strings.DiskAnalyzer_Scanning;
            OverlayPanel.Visibility = Visibility.Visible;
            _entries.Clear();
            SetRunning(true);

            List<DiskEntry> result = new();
            try
            {
                result = await Task.Run(() => DiskAnalyzerService.Analyze(path, token), token);
            }
            catch (System.OperationCanceledException) { SetRunning(false); return; }

            if (token.IsCancellationRequested) { SetRunning(false); return; }

            foreach (var en in result) _entries.Add(en);
            OverlayPanel.Visibility = Visibility.Collapsed;
            StatusText.Text = string.Format(Strings.DiskAnalyzer_Status, result.Count);
            SetRunning(false);
        }

        private void SetRunning(bool running)
        {
            _isRunning = running;
            StartButton.IsEnabled = !running;
            StopButton.IsEnabled = running;
            DriveCombo.IsEnabled = !running;
        }

        private async void Entry_Click(object sender, MouseButtonEventArgs e)
        {
            if (_isRunning) return;
            if (sender is FrameworkElement fe && fe.Tag is DiskEntry entry && entry.IsDirectory)
                await AnalyzeAsync(entry.FullPath);
        }

        private async void Up_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;
            try
            {
                var parent = Directory.GetParent(_currentPath);
                if (parent != null) await AnalyzeAsync(parent.FullName);
            }
            catch { }
        }
    }
}
