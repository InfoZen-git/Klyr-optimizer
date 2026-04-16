using System.Windows;
using InfoZen.Services;

namespace InfoZen
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Capture toutes les exceptions non gérées
            ErrorHandler.Register();

            // Charge les paramètres utilisateur
            _ = SettingsService.Current;
            ThemeService.ApplyTheme(SettingsService.Current.Theme);

            LogService.Instance.Info("InfoZen v2.1 démarré.", "App");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Sauvegarde les paramètres à la fermeture
            SettingsService.Save();
            base.OnExit(e);
        }
    }
}
