using Klyr.Models;

namespace Klyr.Services
{
    /// <summary>
    /// v2.5.0 — Débloat UWP avancé. Liste les applications du Windows Store désinstallables
    /// pour l'utilisateur courant (hors frameworks et apps non supprimables) et les retire via
    /// Remove-AppxPackage. Réversible : les apps restent réinstallables depuis le Microsoft Store.
    /// N'exige pas l'élévation (portée utilisateur).
    /// </summary>
    public static class AppxService
    {
        public static async Task<List<AppxEntry>> GetInstalledAsync()
        {
            var list = new List<AppxEntry>();
            try
            {
                // Sortie ligne à ligne : Name|PackageFullName|Publisher
                string script = @"
                    Get-AppxPackage |
                        Where-Object { -not $_.IsFramework -and -not $_.NonRemovable -and $_.SignatureKind -ne 'System' } |
                        ForEach-Object { ""$($_.Name)|$($_.PackageFullName)|$($_.Publisher)"" }
                ";
                var result = await SystemService.RunPowerShellAsync(script);
                foreach (var raw in result.Output.Split('\n'))
                {
                    string line = raw.Trim();
                    if (line.Length == 0) continue;
                    var parts = line.Split('|');
                    if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1])) continue;

                    list.Add(new AppxEntry
                    {
                        Name            = parts[0].Trim(),
                        PackageFullName = parts[1].Trim(),
                        Publisher       = parts.Length > 2 ? parts[2].Trim() : ""
                    });
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Lecture des applications UWP échouée : {ex.Message}", "Système");
            }

            return list.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Désinstalle une app (portée utilisateur). Retourne true si succès. Best-effort.</summary>
        public static async Task<bool> UninstallAsync(AppxEntry entry)
        {
            try
            {
                string safe = entry.PackageFullName.Replace("'", "''");
                string script = $@"
                    try {{ Remove-AppxPackage -Package '{safe}' -ErrorAction Stop; Write-Output 'OK' }}
                    catch {{ Write-Output ""ERR: $($_.Exception.Message)"" }}
                ";
                var r = await SystemService.RunPowerShellAsync(script);
                return r.Success && r.Output.Contains("OK") && !r.Output.Contains("ERR:");
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Désinstallation UWP échouée ({entry.Name}) : {ex.Message}", "Système");
                return false;
            }
        }
    }
}
