using System.IO;
using Klyr.Models;
using Microsoft.Win32;

namespace Klyr.Services
{
    /// <summary>
    /// v2.5.0 (fix) — Startup Manager aligné sur le comportement du Gestionnaire des tâches Windows.
    ///
    /// Corrections des bugs remontés :
    ///   • Affiche TOUTES les sources : Run HKCU + HKLM (64-bit) + HKLM WOW6432Node (32-bit)
    ///     + dossiers Démarrage (utilisateur et commun).
    ///   • État activé/désactivé lu depuis la clé <c>StartupApproved</c> (le flag binaire que
    ///     Windows utilise réellement), et non plus déduit de la simple présence dans Run.
    ///   • Le toggle écrit ce même flag StartupApproved (activé = 02…, désactivé = 03… + horodatage),
    ///     donc parfaitement cohérent avec le Gestionnaire des tâches. Aucune entrée n'est déplacée.
    ///
    /// Note : modifier les entrées HKLM (machine) écrit dans HKLM\...\StartupApproved et nécessite
    /// l'élévation ; sans admin, l'écriture échoue proprement (le toggle renvoie false).
    /// </summary>
    public static class StartupManagerService
    {
        private const string RunPath     = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string RunPath32   = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run";
        private const string ApprovedRun       = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
        private const string ApprovedRun32     = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run32";
        private const string ApprovedFolder    = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

        private static string StartupFolderUser   => Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        private static string StartupFolderCommon => Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup);

        public static List<StartupEntry> GetStartupEntries()
        {
            var list = new List<StartupEntry>();

            ReadRunKey(Registry.CurrentUser,  RunPath,   StartupSource.RegistryHKCU,   "Registre (HKCU)",        list);
            ReadRunKey(Registry.LocalMachine, RunPath,   StartupSource.RegistryHKLM,   "Registre (HKLM 64-bit)", list);
            ReadRunKey(Registry.LocalMachine, RunPath32, StartupSource.RegistryHKLM32, "Registre (HKLM 32-bit)", list);

            ReadStartupFolder(StartupFolderUser,   StartupSource.FolderUser,   "Dossier démarrage",        list);
            ReadStartupFolder(StartupFolderCommon, StartupSource.FolderCommon, "Dossier démarrage (tous)", list);

            return list.OrderBy(e => e.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // ─────────────────────────── LECTURE ────────────────────────────
        private static void ReadRunKey(RegistryKey hive, string path, StartupSource source, string label, List<StartupEntry> acc)
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
                        Name      = valName,
                        Command   = cmd,
                        Location  = label,
                        Source    = source,
                        IsEnabled = IsApprovedEnabled(source, valName)
                    });
                }
            }
            catch { /* clé inaccessible */ }
        }

        private static void ReadStartupFolder(string folder, StartupSource source, string label, List<StartupEntry> acc)
        {
            try
            {
                if (!Directory.Exists(folder)) return;
                foreach (var lnk in Directory.GetFiles(folder, "*.lnk"))
                {
                    string fileName = Path.GetFileName(lnk);
                    acc.Add(new StartupEntry
                    {
                        Name         = Path.GetFileNameWithoutExtension(lnk),
                        Command      = lnk,
                        Location     = label,
                        Source       = source,
                        ShortcutPath = lnk,
                        IsEnabled    = IsApprovedEnabled(source, fileName)
                    });
                }
            }
            catch { }
        }

        /// <summary>
        /// Lit l'état depuis StartupApproved : pas d'entrée = activé (défaut) ;
        /// 1er octet pair (0x02/0x06) = activé, impair (0x03) = désactivé.
        /// </summary>
        private static bool IsApprovedEnabled(StartupSource source, string valueName)
        {
            var (hive, approvedPath) = ApprovedLocation(source);
            try
            {
                using var key = hive.OpenSubKey(approvedPath);
                if (key?.GetValue(valueName) is byte[] data && data.Length > 0)
                    return (data[0] & 1) == 0;
            }
            catch { }
            return true; // pas de flag → activé
        }

        // ─────────────────────────── TOGGLE ────────────────────────────
        /// <summary>Active/désactive une entrée via StartupApproved. true si succès.</summary>
        public static bool SetEnabled(StartupEntry entry, bool enable)
        {
            try
            {
                var (hive, approvedPath) = ApprovedLocation(entry.Source);
                string valueName = entry.Source is StartupSource.FolderUser or StartupSource.FolderCommon
                    ? Path.GetFileName(entry.ShortcutPath)
                    : entry.Name;

                using var key = hive.CreateSubKey(approvedPath, writable: true);
                if (key == null) return false;

                byte[] data = new byte[12];
                if (enable)
                {
                    data[0] = 0x02; // activé
                }
                else
                {
                    data[0] = 0x03; // désactivé
                    long ts = DateTime.Now.ToFileTime();
                    Array.Copy(BitConverter.GetBytes(ts), 0, data, 4, 8); // horodatage (comme le Gest. des tâches)
                }

                key.SetValue(valueName, data, RegistryValueKind.Binary);
                entry.IsEnabled = enable;
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                LogService.Instance.Warn($"Modification démarrage refusée (admin requis) : {entry.Name}", "Système");
                return false;
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Toggle démarrage échoué ({entry.Name}) : {ex.Message}", "Système");
                return false;
            }
        }

        // ─────────────────────────── AJOUT (v2.5.0) ────────────────────────────
        /// <summary>
        /// Ajoute un exécutable au démarrage de l'utilisateur (HKCU\...\Run). Sans admin.
        /// Retourne true si l'entrée a été créée.
        /// </summary>
        public static bool AddUserStartup(string exePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath)) return false;
                string name = Path.GetFileNameWithoutExtension(exePath);

                using var key = Registry.CurrentUser.CreateSubKey(RunPath, writable: true);
                if (key == null) return false;
                key.SetValue(name, $"\"{exePath}\"", RegistryValueKind.String);
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Ajout au démarrage échoué : {ex.Message}", "Système");
                return false;
            }
        }

        // ─────────────────────────── HELPERS ────────────────────────────
        private static (RegistryKey hive, string path) ApprovedLocation(StartupSource source) => source switch
        {
            StartupSource.RegistryHKCU   => (Registry.CurrentUser,  ApprovedRun),
            StartupSource.RegistryHKLM   => (Registry.LocalMachine, ApprovedRun),
            StartupSource.RegistryHKLM32 => (Registry.LocalMachine, ApprovedRun32),
            StartupSource.FolderUser     => (Registry.CurrentUser,  ApprovedFolder),
            StartupSource.FolderCommon   => (Registry.LocalMachine, ApprovedFolder),
            _                            => (Registry.CurrentUser,  ApprovedRun)
        };
    }
}
