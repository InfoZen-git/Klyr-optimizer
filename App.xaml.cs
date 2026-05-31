using System.Globalization;
using System.Threading;
using System.Windows;
using Klyr.Resources;
using Klyr.Services;

namespace Klyr
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

            // v2.2.0 — Applique la langue choisie AVANT que la moindre UI ne soit chargée
            ApplyLanguage(SettingsService.Current.Language);

            ThemeService.ApplyTheme(SettingsService.Current.Theme);

            LogService.Instance.Info(Strings.Log_AppStarted, "App");
        }

        /// <summary>
        /// v2.2.0 — Configure la culture UI à partir du setting :
        /// "auto" = culture système, "fr"/"en" = override explicite.
        /// Doit être appelé AVANT toute construction de fenêtre WPF.
        /// </summary>
        private static void ApplyLanguage(string language)
        {
            CultureInfo culture = language?.ToLowerInvariant() switch
            {
                "fr" => new CultureInfo("fr"),
                "en" => new CultureInfo("en"),
                _    => CultureInfo.CurrentUICulture // "auto"
            };

            Thread.CurrentThread.CurrentCulture   = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture   = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            Strings.Culture = culture;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Sauvegarde les paramètres à la fermeture
            SettingsService.Save();
            base.OnExit(e);
        }
    }
}
