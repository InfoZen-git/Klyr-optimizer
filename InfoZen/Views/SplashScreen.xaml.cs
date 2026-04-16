using System.Windows;
using System.Windows.Media.Animation;
using InfoZen.Services;

namespace InfoZen.Views
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
            var steps = new (string message, double width)[]
            {
                ("Vérification des droits…",              56),
                ("Chargement des modules…",               112),
                ("Chargement des paramètres…",            168),
                ("Analyse du système…",                   224),
                ("Prêt !",                                280),
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
