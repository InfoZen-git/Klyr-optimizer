using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace InfoZen.Services
{
    /// <summary>
    /// Gestion centralisée des erreurs non gérées.
    /// Capture tout crash, l'affiche proprement et l'exporte en log.
    /// </summary>
    public static class ErrorHandler
    {
        private static readonly string CrashDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "InfoZen", "CrashLogs");

        public static void Register()
        {
            // Exceptions sur le thread UI
            Application.Current.DispatcherUnhandledException += (_, e) =>
            {
                HandleException(e.Exception, "Thread UI");
                e.Handled = true; // Empêche le crash brutal
            };

            // Exceptions sur les threads background
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                if (e.ExceptionObject is Exception ex)
                    HandleException(ex, "Thread background");
            };

            // Tasks non awaited
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                HandleException(e.Exception, "Task non observée");
                e.SetObserved();
            };
        }

        private static void HandleException(Exception ex, string source)
        {
            try
            {
                string report = BuildReport(ex, source);
                string path   = SaveCrashLog(report);

                LogService.Instance.Error($"[CRASH] {ex.GetType().Name}: {ex.Message}", source);

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    var result = MessageBox.Show(
                        $"Une erreur inattendue s'est produite.\n\n" +
                        $"Type : {ex.GetType().Name}\n" +
                        $"Message : {ex.Message}\n\n" +
                        $"Un rapport a été sauvegardé dans :\n{path}\n\n" +
                        $"Voulez-vous continuer à utiliser InfoZen ?",
                        "Erreur – InfoZen",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Error);

                    if (result == MessageBoxResult.No)
                        Application.Current.Shutdown(1);
                });
            }
            catch (Exception fallbackEx)
            {
                Debug.WriteLine($"[InfoZen][ErrorHandler] Échec gestion exception: {fallbackEx.Message}");
            }
        }

        private static string BuildReport(Exception ex, string source)
        {
            var sb = new StringBuilder();
            sb.AppendLine("════════════════════════════════════════");
            sb.AppendLine("   InfoZen – Rapport de crash");
            sb.AppendLine($"   {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
            sb.AppendLine("════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine($"Source    : {source}");
            sb.AppendLine($"Type      : {ex.GetType().FullName}");
            sb.AppendLine($"Message   : {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Stack trace :");
            sb.AppendLine(ex.StackTrace);

            if (ex.InnerException != null)
            {
                sb.AppendLine();
                sb.AppendLine("Inner exception :");
                sb.AppendLine($"  Type    : {ex.InnerException.GetType().FullName}");
                sb.AppendLine($"  Message : {ex.InnerException.Message}");
                sb.AppendLine(ex.InnerException.StackTrace);
            }

            sb.AppendLine();
            sb.AppendLine("Environnement :");
            sb.AppendLine($"  OS      : {Environment.OSVersion}");
            sb.AppendLine($"  .NET    : {Environment.Version}");
            sb.AppendLine($"  Machine : {Environment.MachineName}");
            sb.AppendLine($"  Compte  : {Environment.UserName}");
            sb.AppendLine($"  Admin   : {IsAdmin()}");

            return sb.ToString();
        }

        private static string SaveCrashLog(string content)
        {
            try
            {
                Directory.CreateDirectory(CrashDir);
                string file = Path.Combine(CrashDir, $"crash_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(file, content, Encoding.UTF8);
                return file;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoZen][ErrorHandler] Échec sauvegarde crash log: {ex.Message}");
                return "(impossible de sauvegarder)";
            }
        }

        private static bool IsAdmin()
        {
            try
            {
                using var identity  = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[InfoZen][ErrorHandler] IsAdmin indisponible: {ex.Message}");
                return false;
            }
        }
    }
}
