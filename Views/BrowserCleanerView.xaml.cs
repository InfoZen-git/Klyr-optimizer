using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Klyr.Services;
using Klyr.Resources;

namespace Klyr.Views
{
    /// <summary>
    /// v2.3.0 — Browser Cleaner intégré. Détection lazy à la 1re visibilité.
    /// </summary>
    public partial class BrowserCleanerView : UserControl
    {
        private readonly ObservableCollection<BrowserTarget> _browsers = new();
        private bool _loadedOnce;

        public BrowserCleanerView()
        {
            InitializeComponent();
            BrowsersList.ItemsSource = _browsers;
            IsVisibleChanged += (_, e) =>
            {
                if (IsVisible && !_loadedOnce) { _loadedOnce = true; Load(); }
            };
        }

        private void Load()
        {
            _browsers.Clear();
            foreach (var b in BrowserCleanerService.DetectBrowsers()) _browsers.Add(b);
            EmptyText.Visibility = _browsers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            StatusText.Text = string.Format(Strings.Browser_Detected, _browsers.Count);
        }

        private async void Clean_Click(object sender, RoutedEventArgs e)
        {
            if (_browsers.Count == 0) return;

            var confirm = MessageBox.Show(
                Strings.Browser_Confirm, Strings.Browser_Title,
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            StatusText.Text = Strings.Browser_Cleaning;
            var targets = _browsers.ToList();
            long freed = await Task.Run(() => BrowserCleanerService.Clean(targets));

            long freedMb = freed / (1024 * 1024);
            StatusText.Text = string.Format(Strings.Browser_Done, freedMb);
            LogService.Instance.Success(string.Format(Strings.Browser_Done, freedMb), "Nettoyage");
        }
    }
}
