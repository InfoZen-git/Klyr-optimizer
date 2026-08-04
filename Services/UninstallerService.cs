using System.Diagnostics;
using System.IO;
using Klyr.Models;
using Microsoft.Win32;

namespace Klyr.Services
{
    /// <summary>
    /// v2.3.0 — Désinstalleur de programmes + détection des restes (inspiré de Kudu).
    /// Lit le registre Uninstall (HKLM 64/32 bits + HKCU), lance la désinstallation,
    /// puis scanne les dossiers/clés résiduels.
    /// </summary>
    public static class UninstallerService
    {
        private static readonly string[] UninstallRoots =
        {
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
            @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
        };

        /// <summary>
        /// Liste tous les programmes installés (dédupliqués par nom).
        /// </summary>
        public static List<InstalledProgram> GetInstalledPrograms()
        {
            var result = new Dictionary<string, InstalledProgram>(StringComparer.OrdinalIgnoreCase);

            // HKLM (machine) — 64 et 32 bits
            foreach (var root in UninstallRoots)
                ReadUninstallKey(Registry.LocalMachine, root, root.Contains("WOW6432"), result);

            // HKCU (utilisateur courant)
            ReadUninstallKey(Registry.CurrentUser,
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", false, result);

            return result.Values
                .Where(p => !string.IsNullOrWhiteSpace(p.Name))
                .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static void ReadUninstallKey(RegistryKey hive, string path, bool isWow64,
            Dictionary<string, InstalledProgram> acc)
        {
            try
            {
                using var key = hive.OpenSubKey(path);
                if (key == null) return;

                foreach (var subName in key.GetSubKeyNames())
                {
                    try
                    {
                        using var sub = key.OpenSubKey(subName);
                        if (sub == null) continue;

                        string name = (sub.GetValue("DisplayName") as string)?.Trim() ?? "";
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        // Ignore les mises à jour Windows et composants système
                        if (sub.GetValue("SystemComponent") is int sc && sc == 1) continue;
                        if (!string.IsNullOrEmpty(sub.GetValue("ParentKeyName") as string)) continue;
                        string releaseType = sub.GetValue("ReleaseType") as string ?? "";
                        if (releaseType.Contains("Update") || releaseType.Contains("Hotfix")) continue;

                        string uninstall = sub.GetValue("UninstallString") as string ?? "";
                        if (string.IsNullOrWhiteSpace(uninstall))
                        {
                            // Beaucoup de produits MSI n'exposent pas de UninstallString explicite :
                            // la sous-clé EST le ProductCode (GUID) → on synthétise msiexec /X{GUID}.
                            // Récupère ces entrées (Windows/Glary les comptent aussi).
                            bool isMsi = (sub.GetValue("WindowsInstaller") is int wi && wi == 1)
                                         || LooksLikeGuid(subName);
                            if (!isMsi) continue;
                            uninstall = $"MsiExec.exe /X{subName}";
                        }

                        long sizeKb = 0;
                        if (sub.GetValue("EstimatedSize") is int es) sizeKb = es;

                        var prog = new InstalledProgram
                        {
                            Name            = name,
                            Version         = sub.GetValue("DisplayVersion") as string ?? "",
                            Publisher       = sub.GetValue("Publisher") as string ?? "",
                            InstallLocation = (sub.GetValue("InstallLocation") as string ?? "").Trim('"'),
                            UninstallString = uninstall,
                            QuietUninstallString = sub.GetValue("QuietUninstallString") as string ?? "",
                            EstimatedSizeKb = sizeKb,
                            RegistryKeyPath = $@"{hive.Name}\{path}\{subName}",
                            IsWow64         = isWow64
                        };

                        // Dédup : garde la 1re occurrence (HKLM prioritaire)
                        if (!acc.ContainsKey(name))
                            acc[name] = prog;
                    }
                    catch { /* clé corrompue, on saute */ }
                }
            }
            catch { /* ruche inaccessible */ }
        }

        /// <summary>
        /// Lance la désinstallation (préfère le mode silencieux si dispo) et attend la fin.
        /// </summary>
        public static async Task<bool> UninstallAsync(InstalledProgram prog, CancellationToken ct = default)
        {
            string cmd = !string.IsNullOrWhiteSpace(prog.QuietUninstallString)
                ? prog.QuietUninstallString
                : prog.UninstallString;
            if (string.IsNullOrWhiteSpace(cmd)) return false;

            try
            {
                // UninstallString est souvent "C:\...\unins000.exe" /SILENT ou MsiExec.exe /X{GUID}
                // On lance via cmd.exe /c pour gérer les arguments embarqués.
                var psi = new ProcessStartInfo
                {
                    FileName        = "cmd.exe",
                    Arguments       = $"/c \"{cmd}\"",
                    UseShellExecute = false,
                    CreateNoWindow  = true
                };
                using var proc = Process.Start(psi);
                if (proc == null) return false;
                await proc.WaitForExitAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Désinstallation échouée ({prog.Name}) : {ex.Message}", "Système");
                return false;
            }
        }

        /// <summary>
        /// Scanne les restes potentiels après désinstallation : dossiers (InstallLocation,
        /// %AppData%, %LocalAppData%, %ProgramData%, Program Files) + clés registre matching le nom.
        /// Retourne la liste des chemins/clés trouvés (rien n'est supprimé ici).
        /// </summary>
        public static List<string> ScanLeftovers(InstalledProgram prog)
        {
            var leftovers = new List<string>();
            var safeName = SanitizeForMatch(prog.Name);
            var publisher = SanitizeForMatch(prog.Publisher);

            // 1) InstallLocation s'il existe encore
            if (!string.IsNullOrWhiteSpace(prog.InstallLocation) && Directory.Exists(prog.InstallLocation)
                && IsSafeToDelete(prog.InstallLocation))
                leftovers.Add(prog.InstallLocation);

            // 2) Dossiers de données applicatives courants
            string[] roots =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
            };

            foreach (var root in roots.Distinct())
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                try
                {
                    foreach (var dir in Directory.EnumerateDirectories(root))
                    {
                        var dirName = SanitizeForMatch(Path.GetFileName(dir));
                        if (dirName.Length < 4) continue;

                        // SÉCURITÉ (FIND-001) : égalité EXACTE uniquement.
                        // Le matching par sous-chaîne provoquait des faux positifs catastrophiques
                        // (ex. désinstaller "Steam" proposait de supprimer "SteamLibrary").
                        // On préfère rater quelques restes plutôt que supprimer des données non liées.
                        bool nameMatch = safeName.Length  >= 4 && dirName == safeName;
                        bool pubMatch  = publisher.Length >= 4 && dirName == publisher;

                        if ((nameMatch || pubMatch) && !leftovers.Contains(dir) && IsSafeToDelete(dir))
                            leftovers.Add(dir);
                    }
                }
                catch { /* dossier protégé */ }
            }

            return leftovers;
        }

        /// <summary>
        /// Supprime les chemins de restes confirmés par l'utilisateur. Retourne (réussis, échoués).
        /// </summary>
        public static (int removed, int failed) DeleteLeftovers(IEnumerable<string> paths)
        {
            int removed = 0, failed = 0;
            foreach (var path in paths)
            {
                // SÉCURITÉ (FIND-001) : garde-fou final avant toute suppression récursive.
                if (!IsSafeToDelete(path)) { failed++; continue; }
                try
                {
                    if (Directory.Exists(path)) { Directory.Delete(path, recursive: true); removed++; }
                    else if (File.Exists(path)) { File.Delete(path); removed++; }
                }
                catch { failed++; }
            }
            return (removed, failed);
        }

        /// <summary>
        /// SÉCURITÉ (FIND-001) — refuse de supprimer un chemin dangereux :
        /// racine de lecteur, dossiers système, racines de profil/Program Files,
        /// ou chemin trop peu profond. Défense en profondeur contre la perte de données.
        /// </summary>
        private static bool IsSafeToDelete(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;

            string full;
            try { full = Path.GetFullPath(path).TrimEnd('\\', '/'); }
            catch { return false; }

            if (string.IsNullOrEmpty(full)) return false;

            // Racine de lecteur (C:\, D:\…)
            var root = Path.GetPathRoot(full)?.TrimEnd('\\', '/');
            if (string.Equals(full, root, StringComparison.OrdinalIgnoreCase)) return false;

            // Profondeur minimale : au moins 2 niveaux sous la racine (ex. C:\X\Y)
            int depth = full.Split('\\', StringSplitOptions.RemoveEmptyEntries).Length;
            if (depth < 3) return false;

            // Liste noire de dossiers critiques (jamais supprimables tels quels)
            string[] criticalFolders =
            {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };
            foreach (var crit in criticalFolders)
            {
                if (string.IsNullOrEmpty(crit)) continue;
                string c = crit.TrimEnd('\\', '/');
                if (string.Equals(full, c, StringComparison.OrdinalIgnoreCase)) return false;
            }

            return true;
        }

        /// <summary>true si le nom de sous-clé ressemble à un ProductCode MSI : {GUID}.</summary>
        private static bool LooksLikeGuid(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 38 || s[0] != '{' || s[^1] != '}') return false;
            return Guid.TryParse(s.Trim('{', '}'), out _);
        }

        private static string SanitizeForMatch(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            // garde lettres/chiffres en minuscule, retire versions/éditeurs génériques
            var cleaned = new string(s.ToLowerInvariant()
                .Where(char.IsLetterOrDigit).ToArray());
            return cleaned;
        }
    }
}
