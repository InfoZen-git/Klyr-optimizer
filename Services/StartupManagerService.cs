using System.IO;
using Klyr.Models;
using Microsoft.Win32;

namespace Klyr.Services
{
    /// <summary>
    /// v2.3.0 — Startup Manager (inspiré de Kudu).
    /// Liste les programmes au démarrage (registre Run HKLM/HKCU + dossiers Startup),
    /// avec activation/désactivation RÉVERSIBLE :
    ///   - Désactiver = déplacer l'entrée vers une clé/dossier de backup Klyr.
    ///   - Activer    = restaurer depuis le backup.
    /// </summary>
    public static class StartupManagerService
    {
        private const string RunPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string BackupHklm = @"SOFTWARE\Klyr\StartupBackup\HKLM";
        private const string BackupHkcu = @"SOFTWARE\Klyr\StartupBackup\HKCU";

        private static string StartupFolderUser =>
            Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        private static string StartupFolderCommon =>
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);
        private static string BackupFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Klyr", "StartupBackup");

        public static List<StartupEntry> GetStartupEntries()
        {
            var list = new List<StartupEntry>();

            // ── Registre : entrées actives ──
            ReadRunKey(Registry.LocalMachine, RunPath, StartupSource.RegistryHKLM, "Registre (HKLM)", true, list);
            ReadRunKey(Registry.CurrentUser,  RunPath, StartupSource.RegistryHKCU, "Registre (HKCU)", true, list);

            // ── Registre : entrées désactivées (backup) ──
            ReadRunKey(Registry.LocalMachine, BackupHklm, StartupSource.RegistryHKLM, "Registre (HKLM)", false, list);
            ReadRunKey(Registry.CurrentUser,  BackupHkcu, StartupSource.RegistryHKCU, "Registre (HKCU)", false, list);

            // ── Dossiers Startup : .lnk actifs ──
            ReadStartupFolder(StartupFolderUser,   "Dossier démarrage", true, list);
            ReadStartupFolder(StartupFolderCommon, "Dossier démarrage (tous)", true, list);

            // ── Dossier backup : .lnk désactivés ──
            ReadStartupFolder(BackupFolder, "Dossier démarrage", false, list);

            return list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static void ReadRunKey(RegistryKey hive, string path, StartupSource source,
            string label, bool enabled, List<StartupEntry> acc)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) return;
                foreach (var valName in key.GetValueNames())
                {
                    if (string.IsNullOrWhiteSpace(valName)) continue;
                    string cmd = key.GetValue(valName) as string ?? "";
                    acc.Add(new StartupEntry
                    {
                        Name = valName,
                        Command = cmd,
                        Location = label,
                        Source = source,
                        RegistryKeyPath = path,
                        IsEnabled = enabled
                    });
                }
            }
            catch { /* clé inaccessible */ }
        }

        private static void ReadStartupFolder(string folder, string label, bool enabled, List<StartupEntry> acc)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var lnk in Directory.GetFiles(folder, "*.lnk"))
                {
                    acc.Add(new StartupEntry
                    {
                        Name = Path.GetFileNameWithoutExtension(lnk),
                        Command = lnk,
                        Location = label,
                        Source = StartupSource.StartupFolder,
                        ShortcutPath = lnk,
                        IsEnabled = enabled
                    });
                }
            }
            catch { }
        }

        /// <summary>
        /// Active ou désactive une entrée. Retourne true si l'opération a réussi.
        /// </summary>
        public static bool SetEnabled(StartupEntry entry, bool enable)
        {
            try
            {
                if (entry.Source == StartupSource.StartupFolder)
                    return ToggleFolder(entry, enable);
                return ToggleRegistry(entry, enable);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Startup toggle échoué ({entry.Name}) : {ex.Message}", "Système");
                return false;
            }
        }

        private static bool ToggleRegistry(StartupEntry entry, bool enable)
        {
            bool isHklm = entry.Source == StartupSource.RegistryHKLM;
            RegistryKey hive = isHklm ? Registry.LocalMachine : Registry.CurrentUser;
            string activePath = RunPath;
            string backupPath = isHklm ? BackupHklm : BackupHkcu;

            string from = enable ? backupPath : activePath;
            string to   = enable ? activePath : backupPath;

            using var fromKey = hive.OpenSubKey(from, writable: true);
            if (fromKey == null) return false;
            object? val = fromKey.GetValue(entry.Name);
            if (val == null) return false;

            using var toKey = hive.CreateSubKey(to, writable: true);
            if (toKey == null) return false;

            toKey.SetValue(entry.Name, val, RegistryValueKind.String);
            fromKey.DeleteValue(entry.Name, throwOnMissingValue: false);

            entry.IsEnabled = enable;
            return true;
        }

        private static bool ToggleFolder(StartupEntry entry, bool enable)
        {
            Directory.CreateDirectory(BackupFolder);
            string fileName = Path.GetFileName(entry.ShortcutPath);

            if (enable)
            {
                // backup → dossier Startup utilisateur
                string src = entry.ShortcutPath;
                string dst = Path.Combine(StartupFolderUser, fileName);
                if (!File.Exists(src)) return false;
                File.Move(src, dst, overwrite: true);
                entry.ShortcutPath = dst;
            }
            else
            {
                // Startup → backup
                string src = entry.ShortcutPath;
                string dst = Path.Combine(BackupFolder, fileName);
                if (!File.Exists(src)) return false;
                File.Move(src, dst, overwrite: true);
                entry.ShortcutPath = dst;
            }

            entry.IsEnabled = enable;
            return true;
        }
    }
}
