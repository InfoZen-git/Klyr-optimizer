using System.Security.Principal;
using System.Windows;

namespace InfoZen.Services
{
    /// <summary>
    /// Vérifie les droits admin au démarrage et avertit l'utilisateur
    /// si certaines optimisations ne pourront pas fonctionner.
    /// FIX P2-04: Logging des erreurs au lieu de catch silencieux.
    /// </summary>
    public static class AdminChecker
    {
        public static bool IsRunningAsAdmin()
        {
            try
            {
                using var identity  = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Impossible de vérifier les droits admin : {ex.Message}", "AdminChecker");
                return false;
            }
        }

        /// <summary>
        /// Affiche un avertissement discret (non bloquant) si l'app
        /// n'est pas lancée en admin. Loggé dans le terminal.
        /// </summary>
        public static void CheckAndWarn()
        {
            if (!IsRunningAsAdmin())
            {
                LogService.Instance.Warn(
                    "⚠ InfoZen n'est pas lancé en administrateur. " +
                    "Les optimisations marquées [Admin] seront ignorées ou échoueront. " +
                    "Relancez InfoZen en tant qu'administrateur pour un accès complet.",
                    "Système");
            }
            else
            {
                LogService.Instance.Info("✓ Droits administrateur confirmés.", "Système");
            }
        }
    }
}
