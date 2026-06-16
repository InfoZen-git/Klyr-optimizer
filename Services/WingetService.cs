using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Klyr.Models;

namespace Klyr.Services
{
    /// <summary>
    /// v2.3.0 — Software Updater via winget (inspiré de Kudu).
    /// Parse la sortie tabulaire de `winget upgrade` (colonnes à largeur fixe)
    /// et permet la mise à jour en masse.
    /// </summary>
    public static class WingetService
    {
        /// <summary>True si winget est présent sur la machine.</summary>
        public static async Task<bool> IsAvailableAsync()
        {
            try
            {
                var (exit, _) = await RunWingetAsync("--version", TimeSpan.FromSeconds(10));
                return exit == 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Liste les paquets avec mise à jour disponible.
        /// </summary>
        public static async Task<List<UpgradablePackage>> GetUpgradableAsync(CancellationToken ct = default)
        {
            // --disable-interactivity supprime l'animation de progression (sinon elle pollue
            // la sortie et casse le parsing). Fallback sans l'option si winget est trop ancien.
            var (exit, output) = await RunWingetAsync(
                "upgrade --include-unknown --accept-source-agreements --disable-interactivity",
                TimeSpan.FromMinutes(2), ct);

            var packages = Parse(output);
            if (packages.Count == 0 && !ContainsDashSeparator(output))
            {
                // Retry sans --disable-interactivity (compat anciennes versions de winget)
                var (_, output2) = await RunWingetAsync(
                    "upgrade --include-unknown --accept-source-agreements",
                    TimeSpan.FromMinutes(2), ct);
                packages = Parse(output2);
            }
            return packages;
        }

        /// <summary>
        /// Parse la sortie tabulaire de winget. Robuste, INDÉPENDANT de la langue et insensible
        /// à l'animation de progression : on saute jusqu'après la ligne de tirets, puis on
        /// découpe chaque ligne de données sur les blocs de 2+ espaces. Les colonnes Id/Version/
        /// Disponible/Source ne contiennent jamais d'espace ; seul le Nom en contient (espaces
        /// simples). On mappe donc depuis la DROITE : Source · Disponible · Version · Id · Nom.
        /// </summary>
        private static List<UpgradablePackage> Parse(string output)
        {
            var packages = new List<UpgradablePackage>();
            if (string.IsNullOrWhiteSpace(output)) return packages;

            var lines = output.Split('\n').Select(CleanLine).ToList();

            int dashIdx = FindDashLine(lines);
            if (dashIdx < 0) return packages;

            for (int i = dashIdx + 1; i < lines.Count; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Découpe sur 2+ espaces
                var tokens = Regex.Split(line.Trim(), @"\s{2,}");
                if (tokens.Length < 4) continue;   // ligne de résumé / texte → ignorée

                string name, id, cur, avail;
                if (tokens.Length >= 5)
                {
                    // 5 colonnes : … Id  Version  Disponible  Source (mapping depuis la droite,
                    // robuste même si le Nom contient des doubles espaces)
                    avail = tokens[^2];
                    cur   = tokens[^3];
                    id    = tokens[^4];
                    name  = string.Join(" ", tokens[..^4]);
                }
                else
                {
                    // 4 colonnes : Nom  Id  Version  Disponible (pas de Source)
                    name  = tokens[0];
                    id    = tokens[1];
                    cur   = tokens[2];
                    avail = tokens[3];
                }

                if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(avail)) continue;
                if (avail == cur) continue;
                // L'Id winget ne contient pas d'espace : filtre les fausses lignes
                if (id.Contains(' ')) continue;

                packages.Add(new UpgradablePackage
                {
                    Name             = string.IsNullOrWhiteSpace(name) ? id : name,
                    Id               = id,
                    CurrentVersion   = cur,
                    AvailableVersion = avail
                });
            }

            return packages;
        }

        /// <summary>
        /// Nettoie une ligne capturée : retire les segments réécrits par l'animation
        /// (séparés par \r) en ne gardant que le rendu final, puis trim \r/\n de fin.
        /// </summary>
        private static string CleanLine(string raw)
        {
            string s = raw.TrimEnd('\r', '\n');
            int lastCr = s.LastIndexOf('\r');
            return lastCr >= 0 ? s.Substring(lastCr + 1) : s;
        }

        private static int FindDashLine(List<string> lines)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.Length >= 10 && t.All(c => c == '-')) return i;
            }
            return -1;
        }

        private static bool ContainsDashSeparator(string output)
        {
            if (string.IsNullOrWhiteSpace(output)) return false;
            return FindDashLine(output.Split('\n').Select(CleanLine).ToList()) >= 0;
        }

        /// <summary>
        /// Met à jour un paquet précis (silencieux). Retourne true si exit 0.
        /// </summary>
        public static async Task<bool> UpgradePackageAsync(string id, CancellationToken ct = default)
        {
            var (exit, _) = await RunWingetAsync(
                $"upgrade --id \"{id}\" --silent --accept-package-agreements --accept-source-agreements --disable-interactivity",
                TimeSpan.FromMinutes(10), ct);
            return exit == 0;
        }

        // ─────────────────────────── helpers ────────────────────────────
        private static async Task<(int exitCode, string output)> RunWingetAsync(
            string args, TimeSpan timeout, CancellationToken ct = default)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "winget.exe",
                    Arguments              = args,
                    UseShellExecute        = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding  = Encoding.UTF8
                };

                using var proc = Process.Start(psi);
                if (proc == null) return (-1, "");

                var outputTask = proc.StandardOutput.ReadToEndAsync();
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, ct);

                try
                {
                    await proc.WaitForExitAsync(linked.Token);
                }
                catch (OperationCanceledException)
                {
                    try { proc.Kill(entireProcessTree: true); } catch { }
                    return (-1, await outputTask);
                }

                string output = await outputTask;
                return (proc.ExitCode, output);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"winget indisponible : {ex.Message}", "Système");
                return (-1, "");
            }
        }
    }
}
