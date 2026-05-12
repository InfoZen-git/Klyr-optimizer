using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Klyr.Services;
using Klyr.ViewModels;

namespace Klyr.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            // Enregistre les convertisseurs avant InitializeComponent
            Resources.Add("PageVisibilityConverter", new PageVisibilityConverter());
            Resources.Add("BoolToVisibilityConverter", new BoolToVisibilityConverter());
            Resources.Add("InverseBoolConverter", new InverseBoolConverter());
            Resources.Add("PercentToWidthConverter", new PercentToWidthConverter());
            Resources.Add("PercentToStarConverter", new PercentToStarConverter());
            Resources.Add("AdminBadgeVisibilityConverter", new AdminBadgeVisibilityConverter());

            InitializeComponent();

            // FIX P2-01: Auto-scroll du terminal si activé dans les settings
            TerminalTextBox.TextChanged += TerminalTextBox_TextChanged;

            // Version dans la titlebar
            VersionLabel.Text = $" {DiagnosticService.AppVersion}";
            Title = $"Klyr {DiagnosticService.AppVersion}";
        }

        // FIX P2-01: Auto-scroll du terminal
        private void TerminalTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (SettingsService.Current.AutoScrollTerminal && sender is TextBox textBox)
            {
                textBox.ScrollToEnd();
            }
        }

        // ─── Déplacement de la fenêtre ───

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // FIX P1-05: Disposer le ViewModel avant de fermer
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
            Application.Current.Shutdown();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var win = new SettingsWindow { Owner = this };
            win.ShowDialog();
        }

        private void BtnLegal_Click(object sender, RoutedEventArgs e)
        {
            var win = new LegalWindow { Owner = this };
            win.ShowDialog();
        }

        private void BtnAbout_Click(object sender, RoutedEventArgs e)
        {
            var win = new AboutWindow { Owner = this };
            win.ShowDialog();
        }
    }

    // ═══════════════════════════════ CONVERTISSEURS ═══════════════════════════════

    /// <summary>
    /// Affiche Dashboard / Terminal ou Module selon la page courante.
    /// ConverterParameter = "Dashboard" | "Terminal" | "Module"
    /// </summary>
    public class PageVisibilityConverter : IValueConverter
    {
        private static readonly HashSet<string> ModulePages =
            new() { "Gaming", "OldPC", "Cleaning", "Network" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string page  = value?.ToString() ?? "";
            string param = parameter?.ToString() ?? "";

            return param switch
            {
                "Dashboard" => page == "Dashboard"      ? Visibility.Visible : Visibility.Collapsed,
                "Terminal"  => page == "Terminal"        ? Visibility.Visible : Visibility.Collapsed,
                "Module"    => ModulePages.Contains(page)? Visibility.Visible : Visibility.Collapsed,
                _           => Visibility.Collapsed
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>bool → Visibility</summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is true ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>Inverse d'un bool (pour IsEnabled sur bouton en cours)</summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// Convertit un pourcentage (0–100) en largeur pixel.
    /// ConverterParameter = largeur max en pixels.
    /// </summary>
    /// <summary>
    /// FIX P2-09: convertit un pourcentage (0-100) en GridLength * pour des barres responsives.
    /// Utiliser ConverterParameter="rest" pour obtenir le complément (100 - percent).
    /// </summary>
    public class PercentToStarConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = value switch
            {
                float f        => f,
                double d       => d,
                int i          => i,
                long l         => l,
                IConvertible c => c.ToDouble(culture),
                _              => 0.0
            };
            percent = Math.Max(0, Math.Min(100, percent));
            bool rest = (parameter as string)?.Equals("rest", StringComparison.OrdinalIgnoreCase) == true;
            double v = rest ? 100 - percent : percent;
            return new GridLength(v, GridUnitType.Star);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    public class PercentToWidthConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = value switch
            {
                float f        => f,
                double d       => d,
                int i          => i,
                long l         => l,
                IConvertible c => c.ToDouble(culture),
                _              => 0.0
            };
            double maxWidth = parameter switch
            {
                string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out double w) => w,
                double dp => dp,
                int ip    => ip,
                _         => 100.0
            };
            return Math.Max(0, Math.Min(maxWidth, percent / 100.0 * maxWidth));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// FIX P2-01: Affiche le badge Admin uniquement si RequiresAdmin ET ShowAdminBadge activé.
    /// Vérifie le setting depuis SettingsService.
    /// </summary>
    public class AdminBadgeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool requiresAdmin = value is true;
            bool showBadge = SettingsService.Current.ShowAdminBadge;
            return (requiresAdmin && showBadge) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}