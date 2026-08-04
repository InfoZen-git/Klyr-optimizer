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
        // v2.4.0 — Mode arrière-plan (system tray)
        private TrayIconService? _tray;
        private bool _reallyClosing;        // true quand l'utilisateur quitte vraiment
        private bool _trayHintShown;        // n'affiche le ballon « réduit » qu'une fois

        public MainWindow()
        {
            // v2.3.0 — Les convertisseurs sont désormais déclarés en portée globale dans App.xaml
            // (accessibles depuis MainWindow ET tous les UserControls/DataTemplates).
            InitializeComponent();

            // FIX P2-01: Auto-scroll du terminal si activé dans les settings
            TerminalTextBox.TextChanged += TerminalTextBox_TextChanged;

            // Version dans la titlebar
            VersionLabel.Text = $" {DiagnosticService.AppVersion}";
            Title = $"Klyr {DiagnosticService.AppVersion}";

            // v2.2.0 — Adapter le Border aux changements d'état (maximize vs normal)
            StateChanged += OnWindowStateChanged;

            // v2.4.0 — Icône de zone de notification (créée une fois, masquée par défaut)
            InitializeTray();
        }

        // ─────────────────────── SYSTEM TRAY (v2.4.0) ───────────────────────
        private void InitializeTray()
        {
            try
            {
                _tray = new TrayIconService(
                    $"Klyr {DiagnosticService.AppVersion}",
                    Klyr.Resources.Strings.Tray_Open,
                    Klyr.Resources.Strings.Tray_QuickClean,
                    Klyr.Resources.Strings.Tray_Quit);

                _tray.OpenRequested       += ShowFromTray;
                _tray.QuickCleanRequested += TrayQuickClean;
                _tray.QuitRequested       += QuitFromTray;
            }
            catch { _tray = null; /* tray indisponible : l'app reste utilisable normalement */ }
        }

        private void HideToTray()
        {
            if (_tray == null) { WindowState = WindowState.Minimized; return; }
            _tray.Show();
            Hide();                       // retire de la barre des tâches
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _tray.ShowBalloon("Klyr", Klyr.Resources.Strings.Tray_MinimizedHint);
            }
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            Topmost = true; Topmost = false;   // amène au premier plan
            _tray?.Hide();
        }

        private async void TrayQuickClean()
        {
            _tray?.ShowBalloon("Klyr", Klyr.Resources.Strings.Tray_Cleaning);
            long freed = await System.Threading.Tasks.Task.Run(() =>
            {
                try { return SilentCleanupService.Run(); } catch { return 0L; }
            });
            long mb = freed / (1024 * 1024);
            _tray?.ShowBalloon("Klyr", string.Format(Klyr.Resources.Strings.Tray_CleanDone, mb));
        }

        private void QuitFromTray()
        {
            _reallyClosing = true;
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
            _tray?.Dispose();
            Application.Current.Shutdown();
        }

        // v2.4.0 — Filet de sécurité : libère l'icône tray quel que soit le chemin de fermeture
        // (bouton fermer, Alt+F4, arrêt session…) pour éviter une icône fantôme.
        protected override void OnClosed(EventArgs e)
        {
            _tray?.Dispose();
            base.OnClosed(e);
        }

        /// <summary>
        /// v2.2.0 — Quand la fenêtre est maximisée, on retire les coins arrondis et la dropshadow
        /// (sinon coins triangulaires invisibles + shadow off-screen). Margin=7 compense le surdimensionnement
        /// que WindowChrome applique en mode Maximized avec AllowsTransparency=True.
        /// </summary>
        private void OnWindowStateChanged(object? sender, EventArgs e)
        {
            // v2.4.0 — Réduction dans le tray si l'option est activée
            if (WindowState == WindowState.Minimized && SettingsService.Current.MinimizeToTray)
            {
                HideToTray();
                return;
            }

            if (WindowState == WindowState.Maximized)
            {
                OuterBorder.CornerRadius   = new CornerRadius(0);
                OuterBorder.BorderThickness = new Thickness(0);
                OuterBorder.Margin         = new Thickness(7);
                OuterBorder.Effect         = null;
            }
            else
            {
                OuterBorder.CornerRadius    = new CornerRadius(10);
                OuterBorder.BorderThickness = new Thickness(1);
                OuterBorder.Margin          = new Thickness(0);
                OuterBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color       = System.Windows.Media.Colors.Black,
                    BlurRadius  = 24,
                    ShadowDepth = 0,
                    Opacity     = 0.6
                };
            }
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
            // v2.4.0 — Si le mode arrière-plan est activé, le bouton fermer réduit dans le tray
            // au lieu de quitter. Le vrai « Quitter » est dans le menu de l'icône tray.
            if (!_reallyClosing && SettingsService.Current.MinimizeToTray && _tray != null)
            {
                HideToTray();
                return;
            }

            // FIX P1-05: Disposer le ViewModel avant de fermer
            if (DataContext is IDisposable disposable)
                disposable.Dispose();
            _tray?.Dispose();
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
            new() { "Gaming", "OldPC", "Cleaning", "Network", "Streaming", "Privacy" };

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string page  = value?.ToString() ?? "";
            string param = parameter?.ToString() ?? "";

            return param switch
            {
                "Dashboard" => page == "Dashboard"      ? Visibility.Visible : Visibility.Collapsed,
                "Terminal"  => page == "Terminal"        ? Visibility.Visible : Visibility.Collapsed,
                "Module"    => ModulePages.Contains(page)? Visibility.Visible : Visibility.Collapsed,
                // v2.3.0 — pages outils intégrées : match exact sur le nom de page
                _           => page == param             ? Visibility.Visible : Visibility.Collapsed
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// v2.3.0 — Convertit un score 0-100 en arc circulaire (jauge style VoltAir).
    /// Cercle de rayon 54 centré en (64,64), départ en haut, sens horaire.
    /// </summary>
    public class ScoreToArcConverter : IValueConverter
    {
        private const double Cx = 64, Cy = 64, R = 54;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double pct = value switch
            {
                int i => i,
                double d => d,
                float f => f,
                _ => 0
            };
            pct = Math.Clamp(pct, 0, 100);

            var geo = new System.Windows.Media.StreamGeometry();
            if (pct <= 0) return geo;

            // Évite l'arc 360° exact (ArcSegment ne dessine rien) en clampant à 359.9°
            double sweep = Math.Min(pct / 100.0 * 360.0, 359.9);
            double startAngle = -90;                       // haut
            double endAngle = startAngle + sweep;

            System.Windows.Point P(double angleDeg)
            {
                double a = angleDeg * Math.PI / 180.0;
                return new System.Windows.Point(Cx + R * Math.Cos(a), Cy + R * Math.Sin(a));
            }

            var start = P(startAngle);
            var end = P(endAngle);
            bool largeArc = sweep > 180;

            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(start, isFilled: false, isClosed: false);
                ctx.ArcTo(end, new System.Windows.Size(R, R), 0,
                    largeArc, System.Windows.Media.SweepDirection.Clockwise,
                    isStroked: true, isSmoothJoin: false);
            }
            geo.Freeze();
            return geo;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// v2.3.0 — État actif du sidebar : fond accentué subtil si CurrentPage == ConverterParameter.
    /// </summary>
    public class NavActiveBgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool active = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);
            if (!active) return System.Windows.Media.Brushes.Transparent;
            return Application.Current.TryFindResource("AccentSubtleBrush")
                   ?? (object)System.Windows.Media.Brushes.Transparent;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// v2.3.0 — Texte accentué pour l'item de nav actif (sinon couleur secondaire).
    /// </summary>
    public class NavActiveFgConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool active = string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);
            string key = active ? "AccentBrush" : "TextSecondaryBrush";
            return Application.Current.TryFindResource(key)
                   ?? (object)System.Windows.Media.Brushes.Gray;
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