using System.Management;
using Klyr.Models;
using Klyr.Resources;

namespace Klyr.Services
{
    /// <summary>
    /// v2.4.0 — Gestionnaire de services Windows.
    /// Expose une liste CURÉE de services non essentiels (télémétrie, confort)
    /// que l'on peut désactiver sans casser le système. Toggle RÉVERSIBLE :
    ///   - Désactiver = StartupType Disabled + arrêt du service.
    ///   - Activer    = restauration du type de démarrage par défaut connu.
    /// Lecture sans admin ; modification nécessite l'élévation.
    /// </summary>
    public static class ServicesManagerService
    {
        /// <summary>Définition d'un service curé : nom court + type de démarrage par défaut.</summary>
        private sealed record Curated(string Name, string DefaultStartMode);

        // Liste volontairement conservatrice : uniquement des services dont la
        // désactivation est documentée comme sûre (pas de services critiques,
        // pas de services Xbox pour ne pas gêner le module Gaming).
        private static readonly Curated[] CuratedServices =
        {
            new("DiagTrack",         "Automatic"), // Connected User Experiences and Telemetry
            new("dmwappushservice",  "Manual"),    // WAP Push (transport télémétrie)
            new("Fax",               "Manual"),    // Fax
            new("RetailDemo",        "Manual"),    // Mode démo magasin
            new("RemoteRegistry",    "Manual"),    // Registre distant (surface d'attaque)
            new("MapsBroker",        "Automatic"), // Gestionnaire de cartes téléchargées
            new("WMPNetworkSvc",     "Manual"),    // Partage réseau Windows Media Player
            new("WerSvc",            "Manual"),    // Rapport d'erreurs Windows
            new("lfsvc",             "Manual"),    // Service de géolocalisation
        };

        private static readonly Dictionary<string, string> DefaultModeByName =
            CuratedServices.ToDictionary(c => c.Name, c => c.DefaultStartMode, StringComparer.OrdinalIgnoreCase);

        public static List<ServiceEntry> GetServices()
        {
            var result = new List<ServiceEntry>();
            var found = new Dictionary<string, (string display, string state, string startMode)>(StringComparer.OrdinalIgnoreCase);

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DisplayName, State, StartMode FROM Win32_Service");
                foreach (ManagementObject mo in searcher.Get())
                {
                    string name = (mo["Name"] as string) ?? "";
                    if (string.IsNullOrEmpty(name) || !DefaultModeByName.ContainsKey(name)) continue;
                    found[name] = (
                        (mo["DisplayName"] as string) ?? name,
                        (mo["State"] as string) ?? "",
                        (mo["StartMode"] as string) ?? "");
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Lecture des services échouée : {ex.Message}", "Système");
            }

            foreach (var c in CuratedServices)
            {
                if (!found.TryGetValue(c.Name, out var info))
                    continue; // service absent sur cette édition de Windows

                bool disabled = string.Equals(info.startMode, "Disabled", StringComparison.OrdinalIgnoreCase);
                result.Add(new ServiceEntry
                {
                    Name             = c.Name,
                    DisplayName      = info.display,
                    Description      = DescribeService(c.Name),
                    DefaultStartMode = c.DefaultStartMode,
                    IsRunning        = string.Equals(info.state, "Running", StringComparison.OrdinalIgnoreCase),
                    IsEnabled        = !disabled
                });
            }

            return result.OrderBy(e => e.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>
        /// Active (restaure le type par défaut) ou désactive (Disabled + stop) un service.
        /// Nécessite l'élévation. Best-effort, ne throw jamais.
        /// </summary>
        public static async Task<bool> SetEnabledAsync(ServiceEntry entry, bool enable)
        {
            try
            {
                string mode = entry.DefaultStartMode == "Automatic" ? "Automatic" : "Manual";
                string script = enable
                    ? $@"Set-Service -Name '{entry.Name}' -StartupType {mode} -ErrorAction Stop
                         if ('{mode}' -eq 'Automatic') {{ Start-Service -Name '{entry.Name}' -ErrorAction SilentlyContinue }}
                         Write-Output 'OK'"
                    : $@"Stop-Service -Name '{entry.Name}' -Force -ErrorAction SilentlyContinue
                         Set-Service -Name '{entry.Name}' -StartupType Disabled -ErrorAction Stop
                         Write-Output 'OK'";

                var r = await SystemService.RunPowerShellAsync(script, asAdmin: true);
                bool ok = r.Success && r.Output.Contains("OK");
                if (ok) entry.IsEnabled = enable;
                return ok;
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Toggle service échoué ({entry.Name}) : {ex.Message}", "Système");
                return false;
            }
        }

        private static string DescribeService(string name) => name switch
        {
            "DiagTrack"        => Strings.Service_DiagTrack_Desc,
            "dmwappushservice" => Strings.Service_dmwappushservice_Desc,
            "Fax"              => Strings.Service_Fax_Desc,
            "RetailDemo"       => Strings.Service_RetailDemo_Desc,
            "RemoteRegistry"   => Strings.Service_RemoteRegistry_Desc,
            "MapsBroker"       => Strings.Service_MapsBroker_Desc,
            "WMPNetworkSvc"    => Strings.Service_WMPNetworkSvc_Desc,
            "WerSvc"           => Strings.Service_WerSvc_Desc,
            "lfsvc"            => Strings.Service_lfsvc_Desc,
            _                  => name
        };
    }
}
