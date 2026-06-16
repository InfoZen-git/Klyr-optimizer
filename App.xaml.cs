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
            // v2.3.0 — Mode scan programmé (headless) : nettoyage silencieux puis fermeture.
            // Vérifié AVANT base.OnStartup pour éviter d'afficher le SplashScreen (StartupUri).
            if (e.Args.Any(a => string.Equals(a, ScheduledScanService.CleanArg, StringComparison.OrdinalIgnoreCase)))
            {
                try { SilentCleanupService.Run(); }
                catch { /* best-effort */ }
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // Capture toutes les exceptions non gérées
            ErrorHandler.Register();

            // Charge les paramètres utilisateur
            _ = SettingsService.Current;

            // v2.2.0 — Applique la langue choisie AVANT que la moindre UI ne soit chargée
            ApplyLanguage(SettingsService.Current.Language);

            ThemeService.ApplyTheme(SettingsService.Current.Theme);

            LogService.Instance.Info(string.Format(Strings.Log_AppStarted, "v" + DiagnosticService.AppVersion), "App");
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
