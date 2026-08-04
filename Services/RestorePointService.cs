using System.Diagnostics;
using System.Management;
using Klyr.Models;

namespace Klyr.Services
{
    /// <summary>
    /// v2.4.0 — Gestionnaire de points de restauration système.
    /// Lecture via WMI (root\default → SystemRestore). Création déléguée à
    /// SystemService. La restauration ouvre l'assistant Windows (rstrui) — on ne
    /// restaure jamais en silence. Suppression via vssadmin (libère de l'espace).
    /// </summary>
    public static class RestorePointService
    {
        public static List<RestorePointEntry> GetRestorePoints()
        {
            var list = new List<RestorePointEntry>();
            try
            {
                var scope = new ManagementScope(@"\\.\root\default");
                scope.Connect();
                var query = new ObjectQuery("SELECT * FROM SystemRestore");
                using var searcher = new ManagementObjectSearcher(scope, query);
                foreach (ManagementObject mo in searcher.Get())
                {
                    DateTime created = DateTime.MinValue;
                    try
                    {
                        string raw = mo["CreationTime"] as string ?? "";
                        if (!string.IsNullOrEmpty(raw))
                            created = ManagementDateTimeConverter.ToDateTime(raw);
                    }
                    catch { /* format inattendu */ }

                    list.Add(new RestorePointEntry
                    {
                        SequenceNumber   = Convert.ToUInt32(mo["SequenceNumber"] ?? 0u),
                        Description      = mo["Description"] as string ?? "",
                        RestorePointType = Convert.ToInt32(mo["RestorePointType"] ?? 0),
                        CreationTime     = created
                    });
                }
            }
            catch (Exception ex)
            {
                // Souvent : pas admin, ou protection système désactivée.
                LogService.Instance.Warn($"Lecture des points de restauration échouée : {ex.Message}", "Système");
            }

            return list.OrderByDescending(e => e.CreationTime).ToList();
        }

        /// <summary>Crée un point de restauration. Nécessite l'élévation.</summary>
        public static async Task<bool> CreateAsync(string description)
        {
            var r = await SystemService.CreateRestorePointAsync(description);
            return r.Success;
        }

        /// <summary>Ouvre l'assistant de restauration système Windows (rstrui.exe).</summary>
        public static bool OpenSystemRestore()
        {
            try
            {
                Process.Start(new ProcessStartInfo { FileName = "rstrui.exe", UseShellExecute = true });
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Ouverture de la restauration système échouée : {ex.Message}", "Système");
                return false;
            }
        }

        /// <summary>
        /// Supprime TOUS les points de restauration du lecteur système (libère de l'espace).
        /// Nécessite l'élévation. Best-effort.
        /// </summary>
        public static async Task<bool> DeleteAllAsync()
        {
            var r = await SystemService.RunCmdAsync("vssadmin delete shadows /for=C: /all /quiet", asAdmin: true);
            return r.Success;
        }
    }
}
