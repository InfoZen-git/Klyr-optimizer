using System.IO;
using System.Management;
using Klyr.Models;
using Klyr.Resources;
using Microsoft.Win32;

namespace Klyr.Services
{
    /// <summary>
    /// v2.5.0 — Rapport de santé : une série de vérifications rapides et sans risque
    /// qui produisent des recommandations concrètes, chacune reliée à l'outil/module qui
    /// permet de corriger. Best-effort : chaque check est isolé et ne fait jamais planter.
    /// </summary>
    public static class HealthReportService
    {
        public static List<HealthCheck> Run()
        {
            var list = new List<HealthCheck>();

            var stats = SafeStats();

            // ── RAM ──
            double ram = stats.RamPercent;
            list.Add(new HealthCheck
            {
                Title    = Strings.Health_Ram_Title,
                Detail   = string.Format(Strings.Health_Ram_Detail, ram.ToString("F0")),
                Severity = ram >= 85 ? HealthSeverity.Critical : ram >= 70 ? HealthSeverity.Warning : HealthSeverity.Good,
                ActionPage = ram >= 70 ? "OldPC" : ""
            });

            // ── Disque système ──
            double freePct = stats.DiskTotalGb > 0 ? stats.DiskFreeGb / stats.DiskTotalGb * 100.0 : 100;
            list.Add(new HealthCheck
            {
                Title    = Strings.Health_Disk_Title,
                Detail   = string.Format(Strings.Health_Disk_Detail, stats.DiskFreeGb.ToString("F0"), freePct.ToString("F0")),
                Severity = freePct < 10 ? HealthSeverity.Critical : freePct < 20 ? HealthSeverity.Warning : HealthSeverity.Good,
                ActionPage = freePct < 20 ? "Cleaning" : ""
            });

            // ── Fichiers temporaires ──
            long tempBytes = TempSize();
            double tempGb = tempBytes / (1024.0 * 1024 * 1024);
            list.Add(new HealthCheck
            {
                Title    = Strings.Health_Temp_Title,
                Detail   = string.Format(Strings.Health_Temp_Detail, FormatSize(tempBytes)),
                Severity = tempGb >= 5 ? HealthSeverity.Critical : tempGb >= 2 ? HealthSeverity.Warning : HealthSeverity.Good,
                ActionPage = tempGb >= 2 ? "Cleaning" : ""
            });

            // ── Télémétrie ──
            bool diag = IsServiceRunning("DiagTrack");
            list.Add(new HealthCheck
            {
                Title    = Strings.Health_Telemetry_Title,
                Detail   = diag ? Strings.Health_Telemetry_On : Strings.Health_Telemetry_Off,
                Severity = diag ? HealthSeverity.Warning : HealthSeverity.Good,
                ActionPage = diag ? "Privacy" : ""
            });

            // ── Programmes au démarrage ──
            int startup = StartupCount();
            list.Add(new HealthCheck
            {
                Title    = Strings.Health_Startup_Title,
                Detail   = string.Format(Strings.Health_Startup_Detail, startup),
                Severity = startup > 12 ? HealthSeverity.Critical : startup > 8 ? HealthSeverity.Warning : HealthSeverity.Good,
                ActionPage = startup > 8 ? "Startup" : ""
            });

            // ── Point de restauration (lecture nécessitant l'admin ; -1 = inconnu) ──
            int restore = RestoreCount();
            list.Add(new HealthCheck
            {
                Title    = Strings.Health_Restore_Title,
                Detail   = restore < 0 ? Strings.Health_Restore_Unknown
                         : restore == 0 ? Strings.Health_Restore_None
                         : string.Format(Strings.Health_Restore_Ok, restore),
                Severity = restore == 0 ? HealthSeverity.Warning : HealthSeverity.Good,
                ActionPage = restore == 0 ? "Restore" : ""
            });

            return list;
        }

        // ─────────────────────────── HELPERS ────────────────────────────
        private static (double RamPercent, double DiskFreeGb, double DiskTotalGb) SafeStats()
        {
            try
            {
                var s = SystemService.GetRealtimeStats();
                return (s.RamPercent, s.DiskFreeGb, s.DiskTotalGb);
            }
            catch { return (0, 0, 0); }
        }

        private static long TempSize()
        {
            long total = 0;
            var opts = new System.IO.EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible    = true,
                AttributesToSkip      = FileAttributes.ReparsePoint
            };
            foreach (var dir in new[] { Path.GetTempPath(), @"C:\Windows\Temp" })
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.EnumerateFiles(dir, "*", opts))
                    {
                        try { total += new FileInfo(f).Length; } catch { }
                    }
                }
                catch { }
            }
            return total;
        }

        private static bool IsServiceRunning(string name)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT State FROM Win32_Service WHERE Name='{name}'");
                foreach (ManagementObject mo in searcher.Get())
                    return string.Equals(mo["State"] as string, "Running", StringComparison.OrdinalIgnoreCase);
            }
            catch { }
            return false;
        }

        private static int StartupCount()
        {
            int count = 0;
            count += CountRunValues(Registry.CurrentUser);
            count += CountRunValues(Registry.LocalMachine);
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (Directory.Exists(startupFolder))
                    count += Directory.GetFiles(startupFolder, "*.lnk").Length;
            }
            catch { }
            return count;
        }

        private static int CountRunValues(RegistryKey hive)
        {
            try
            {
                using var key = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run");
                return key?.GetValueNames().Count(v => !string.IsNullOrWhiteSpace(v)) ?? 0;
            }
            catch { return 0; }
        }

        private static int RestoreCount()
        {
            // Sans admin la lecture WMI SystemRestore renvoie vide → on marque « inconnu »
            // plutôt que d'afficher un faux « aucun point de restauration ».
            if (!AdminChecker.IsRunningAsAdmin()) return -1;
            try { return RestorePointService.GetRestorePoints().Count; }
            catch { return -1; }
        }

        private static string FormatSize(long b) => b switch
        {
            >= 1073741824 => $"{b / 1073741824.0:F2} Go",
            >= 1048576    => $"{b / 1048576.0:F0} Mo",
            >= 1024       => $"{b / 1024.0:F0} Ko",
            _             => $"{b} o"
        };
    }
}
