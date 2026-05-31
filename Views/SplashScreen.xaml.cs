using System.Windows;
using System.Windows.Media.Animation;
using Klyr.Resources;
using Klyr.Services;

namespace Klyr.Views
{
    public partial class SplashScreen : Window
    {
        public SplashScreen()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // v2.2.0 — Messages localisés
            var steps = new (string message, double width)[]
            {
                (Strings.Splash_CheckAdmin,    56),
                (Strings.Splash_LoadModules,   112),
                (Strings.Splash_LoadSettings,  168),
                (Strings.Splash_AnalyzeSystem, 224),
                (Strings.Splash_Ready,         280),
            };

            foreach (var (message, width) in steps)
            {
                StatusText.Text = message;
                AnimateBar(width);
                await Task.Delay(300);
            }

            // Vérification admin en arrière-plan
            await Task.Run(() => AdminChecker.CheckAndWarn());

            await Task.Delay(150);

            var main = new MainWindow();
            // FIX: réassigner Application.MainWindow vers la nouvelle MainWindow,
            // sinon Application.Current.MainWindow reste pointé sur la SplashScreen
            // (qui va être fermée), ce qui casse les bindings cross-window
            // (notamment SettingsWindow.Save_Click → vm.ReloadCurrentOptimizations()).
            Application.Current.MainWindow = main;
            main.Show();
            Close();
        }

        private void AnimateBar(double toWidth)
        {
            var anim = new DoubleAnimation
            {
                To             = toWidth,
                Duration       = TimeSpan.FromMilliseconds(260),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            ProgressBar.BeginAnimation(WidthProperty, anim);
        }
    }
}
