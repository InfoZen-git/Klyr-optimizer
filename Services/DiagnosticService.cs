using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Windows;

namespace Klyr.Services
{
    /// <summary>
    /// Service de diagnostic : génération d'infos système formatées
    /// (presse-papier ou zip) à coller dans un bug report.
    /// </summary>
    public static class DiagnosticService
    {
        /// <summary>Version applicative (lue depuis l'assembly).</summary>
        public static string AppVersion
        {
            get
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "?.?.?";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Infos système formatées (markdown, prêt à coller)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bloc markdown des infos système, à coller dans un bug report.
        /// Volontairement court (~10 lignes).
        /// </summary>
        public static string BuildSystemInfoMarkdown()
        {
            var info = SystemService.GetSystemInfo();
            bool isAdmin = AdminChecker.IsRunningAsAdmin();
            var (ramUsed, ramTotal, ramPct, diskFree, diskTotal, _) = SystemService.GetRealtimeStats();

            var sb = new StringBuilder();
            sb.AppendLine($"**Klyr v{AppVersion}**");
            sb.AppendLine($"- OS : {info.OsName}");
            sb.AppendLine($"- CPU : {info.CpuName}");
            sb.AppendLine($"- RAM : {ramUsed:F1} / {ramTotal:F1} Go ({ramPct:F0}%)");
            sb.AppendLine($"- Disque C: : {diskFree} / {diskTotal} Go libres");
            sb.AppendLine($"- Architecture : {(Environment.Is64BitProcess ? "x64" : "x86")} ({(Environment.Is64BitOperatingSystem ? "OS 64-bit" : "OS 32-bit")})");
            sb.AppendLine($"- Admin : {(isAdmin ? "oui" : "non")}");
            sb.AppendLine($"- Thème : {SettingsService.Current.Theme}");
            sb.AppendLine($"- .NET : {Environment.Version}");
            sb.AppendLine($"- Machine : {info.MachineName}");
            return sb.ToString();
        }

        /// <summary>Copie les infos système dans le presse-papier. Retourne true si succès.</summary>
        public static bool CopySystemInfoToClipboard()
        {
            try
            {
                Clipboard.SetText(BuildSystemInfoMarkdown());
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"Copie presse-papier échouée : {ex.Message}", "Diagnostic");
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Rapport diagnostic complet (zip)
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Génère un zip contenant logs + settings + diagnostic complet,
        /// à joindre à un bug report.
        /// Retourne le chemin du zip ou null si échec.
        /// </summary>
        public static string? ExportDiagnosticReport()
        {
            try
            {
                string reportsDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "Klyr", "Reports");
                Directory.CreateDirectory(reportsDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string zipPath = Path.Combine(reportsDir, $"Klyr_Report_{timestamp}.zip");

                // Dossier temp pour staging avant zip
                string stagingDir = Path.Combine(Path.GetTempPath(), $"klyr_report_{Guid.NewGuid():N}");
                Directory.CreateDirectory(stagingDir);

                try
                {
                    // 1. system_info.txt — diagnostic complet
                    File.WriteAllText(
                        Path.Combine(stagingDir, "system_info.txt"),
                        BuildExtendedDiagnostic(),
                        Encoding.UTF8);

                    // 2. settings.json (s'il existe)
                    string settingsPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "Klyr", "settings.json");
                    if (File.Exists(settingsPath))
                        File.Copy(settingsPath, Path.Combine(stagingDir, "settings.json"));

                    // 3. logs (tous les fichiers .log de Documents\Klyr\Logs)
                    string logsDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "Klyr", "Logs");
                    if (Directory.Exists(logsDir))
                    {
                        string logsTarget = Path.Combine(stagingDir, "logs");
                        Directory.CreateDirectory(logsTarget);
                        foreach (var file in Directory.GetFiles(logsDir, "*.log"))
                        {
                            try { File.Copy(file, Path.Combine(logsTarget, Path.GetFileName(file))); }
                            catch { /* fichier en cours d'écriture */ }
                        }
                    }

                    // 4. terminal_session.txt — buffer terminal en mémoire
                    File.WriteAllText(
                        Path.Combine(stagingDir, "terminal_session.txt"),
                        LogService.Instance.TerminalText,
                        Encoding.UTF8);

                    // 5. README.txt — explique au testeur ce qu'il y a dedans
                    File.WriteAllText(
                        Path.Combine(stagingDir, "README.txt"),
                        BuildReportReadme(),
                        Encoding.UTF8);

                    // Zip final
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    ZipFile.CreateFromDirectory(stagingDir, zipPath);

                    return zipPath;
                }
                finally
                {
                    try { Directory.Delete(stagingDir, recursive: true); } catch { }
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Error($"Export rapport diagnostic échoué : {ex.Message}", "Diagnostic");
                return null;
            }
        }

        /// <summary>Diagnostic étendu — pour le fichier system_info.txt du rapport.</summary>
        private static string BuildExtendedDiagnostic()
        {
            var info = SystemService.GetSystemInfo();
            bool isAdmin = AdminChecker.IsRunningAsAdmin();
            var (ramUsed, ramTotal, ramPct, diskFree, diskTotal, uptime) = SystemService.GetRealtimeStats();
            var settings = SettingsService.Current;

            var sb = new StringBuilder();
            sb.AppendLine("════════════════════════════════════════════════════════");
            sb.AppendLine($"  Klyr v{AppVersion} — Rapport diagnostic");
            sb.AppendLine($"  Généré le : {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("════════════════════════════════════════════════════════");
            sb.AppendLine();
            sb.AppendLine("── SYSTÈME ────────────────────────────────────────────");
            sb.AppendLine($"OS                : {info.OsName}");
            sb.AppendLine($"Build              : {Environment.OSVersion.Version}");
            sb.AppendLine($"Machine            : {info.MachineName}");
            sb.AppendLine($"CPU                : {info.CpuName}");
            sb.AppendLine($"Architecture       : {(Environment.Is64BitProcess ? "x64" : "x86")} (OS {(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
            sb.AppendLine($"RAM totale         : {ramTotal:F1} Go");
            sb.AppendLine($"RAM utilisée       : {ramUsed:F1} Go ({ramPct:F0}%)");
            sb.AppendLine($"Disque C: libre    : {diskFree} Go / {diskTotal} Go");
            sb.AppendLine($"Uptime             : {uptime}");
            sb.AppendLine($".NET runtime       : {Environment.Version}");
            sb.AppendLine($"Process en admin   : {(isAdmin ? "OUI" : "NON")}");
            sb.AppendLine();
            sb.AppendLine("── CAPTEURS MATÉRIELS ─────────────────────────────────");
            try
            {
                var hw = HardwareMonitorService.Instance.Read();
                sb.AppendLine($"Temp CPU           : {(hw.CpuTempC.HasValue ? $"{hw.CpuTempC.Value:F0} °C" : "n/a (admin requis / capteur indisponible)")}");
                sb.AppendLine($"Charge CPU         : {(hw.CpuLoadPct.HasValue ? $"{hw.CpuLoadPct.Value:F0} %" : "n/a")}");
                sb.AppendLine($"GPU                : {hw.GpuName}");
                sb.AppendLine($"Temp GPU           : {(hw.GpuTempC.HasValue ? $"{hw.GpuTempC.Value:F0} °C" : "n/a")}");
                sb.AppendLine($"Usage GPU          : {(hw.GpuLoadPct.HasValue ? $"{hw.GpuLoadPct.Value:F0} %" : "n/a")}");
                sb.AppendLine($"Disque lecture     : {(hw.DiskReadKBs.HasValue ? $"{hw.DiskReadKBs.Value / 1024f:F1} Mo/s" : "n/a")}");
                sb.AppendLine($"Disque écriture    : {(hw.DiskWriteKBs.HasValue ? $"{hw.DiskWriteKBs.Value / 1024f:F1} Mo/s" : "n/a")}");
                sb.AppendLine($"Temp disque        : {(hw.DiskTempC.HasValue ? $"{hw.DiskTempC.Value:F0} °C" : "n/a")}");
                sb.AppendLine($"Capteurs activés   : {settings.EnableHardwareSensors}");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"(lecture capteurs indisponible : {ex.Message})");
            }
            sb.AppendLine();
            sb.AppendLine("── PARAMÈTRES KLYR ────────────────────────────────────");
            sb.AppendLine($"Theme              : {settings.Theme}");
            sb.AppendLine($"LastModule         : {settings.LastModule}");
            sb.AppendLine($"ConfirmBeforeRun   : {settings.ConfirmBeforeRun}");
            sb.AppendLine($"AutoRestorePoint   : {settings.AutoRestorePoint}");
            sb.AppendLine($"SimulationMode     : {settings.SimulationMode}");
            sb.AppendLine($"ShowAdvancedOptims : {settings.ShowAdvancedOptimizations}");
            sb.AppendLine($"AutoSaveLogs       : {settings.AutoSaveLogs}");
            sb.AppendLine();
            sb.AppendLine("── ENVIRONNEMENT ──────────────────────────────────────");
            sb.AppendLine($"User               : {Environment.UserName}");
            sb.AppendLine($"Working dir        : {Environment.CurrentDirectory}");
            sb.AppendLine($"Process path       : {Environment.ProcessPath ?? "?"}");
            sb.AppendLine($"Lang/Culture       : {System.Globalization.CultureInfo.CurrentUICulture.Name}");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildReportReadme()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Klyr — Rapport diagnostic");
            sb.AppendLine("==========================");
            sb.AppendLine();
            sb.AppendLine("Ce zip contient les éléments nécessaires pour qu'un développeur");
            sb.AppendLine("comprenne le contexte d'un bug ou d'un comportement inattendu.");
            sb.AppendLine();
            sb.AppendLine("Contenu :");
            sb.AppendLine("  - system_info.txt     : diagnostic complet de la machine");
            sb.AppendLine("  - settings.json       : tes paramètres Klyr");
            sb.AppendLine("  - logs/               : tous les logs persistants");
            sb.AppendLine("  - terminal_session.txt: buffer terminal de la session courante");
            sb.AppendLine();
            sb.AppendLine("Comment l'utiliser :");
            sb.AppendLine("  1. Joins ce fichier .zip à ton bug report");
            sb.AppendLine("  2. Décris brièvement ce que tu faisais quand le bug est arrivé");
            sb.AppendLine("  3. Précise quelle optimisation tu lançais (le cas échéant)");
            sb.AppendLine();
            sb.AppendLine("Privacy : aucune donnée personnelle n'est collectée. Le zip ne contient");
            sb.AppendLine("que des infos système (OS, CPU, RAM) et les logs Klyr. Tu peux");
            sb.AppendLine("l'inspecter avant de l'envoyer.");
            return sb.ToString();
        }
    }
}
