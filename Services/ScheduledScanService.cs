using System.Diagnostics;
using System.Text;

namespace Klyr.Services
{
    public enum ScanFrequency { Daily, Weekly, Monthly }

    /// <summary>
    /// v2.3.0 — Scans programmés (inspiré de Kudu) via le Planificateur de tâches Windows (schtasks).
    /// Crée une tâche qui relance Klyr avec l'argument --scheduled-clean (nettoyage silencieux).
    /// </summary>
    public static class ScheduledScanService
    {
        public const string TaskName = "Klyr Scheduled Clean";
        public const string CleanArg = "--scheduled-clean";

        public static async Task<bool> IsEnabledAsync()
        {
            var (exit, _) = await RunSchtasksAsync($"/Query /TN \"{TaskName}\"");
            return exit == 0;
        }

        /// <summary>
        /// Crée (ou remplace) la tâche planifiée à 12:00 selon la fréquence choisie.
        /// </summary>
        public static async Task<bool> EnableAsync(ScanFrequency freq)
        {
            string exe = Environment.ProcessPath ?? "";
            if (string.IsNullOrWhiteSpace(exe)) return false;

            string sc = freq switch
            {
                ScanFrequency.Daily   => "DAILY",
                ScanFrequency.Weekly  => "WEEKLY",
                ScanFrequency.Monthly => "MONTHLY",
                _                     => "WEEKLY"
            };

            // /F écrase si existe déjà ; /TR doit échapper les guillemets internes
            string tr = $"\\\"{exe}\\\" {CleanArg}";
            string args = $"/Create /TN \"{TaskName}\" /TR \"{tr}\" /SC {sc} /ST 12:00 /F";

            var (exit, _) = await RunSchtasksAsync(args);
            if (exit == 0)
                LogService.Instance.Success($"Scan programmé activé ({sc}).", "Système");
            return exit == 0;
        }

        public static async Task<bool> DisableAsync()
        {
            var (exit, _) = await RunSchtasksAsync($"/Delete /TN \"{TaskName}\" /F");
            if (exit == 0)
                LogService.Instance.Info("Scan programmé désactivé.", "Système");
            return exit == 0;
        }

        private static async Task<(int, string)> RunSchtasksAsync(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "schtasks.exe",
                    Arguments              = args,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8
                };
                using var proc = Process.Start(psi);
                if (proc == null) return (-1, "");
                string output = await proc.StandardOutput.ReadToEndAsync();
                await proc.WaitForExitAsync();
                return (proc.ExitCode, output);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"schtasks échoué : {ex.Message}", "Système");
                return (-1, "");
            }
        }
    }
}
