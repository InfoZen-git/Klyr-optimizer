using Klyr.Models;
using Klyr.Resources;

namespace Klyr.Services
{
    /// <summary>
    /// v2.5.0 — Profils 1-clic. Agrège les optimisations de tous les modules par ID
    /// et définit des ensembles curés (Gaming, Performance max, Vie privée, Équilibré).
    /// Les profils volontairement n'incluent PAS d'optims critiques (reset réseau/pare-feu,
    /// hosts) ni de passes longues (SFC/DISM, defrag, scan antivirus) pour rester « 1-clic ».
    /// </summary>
    public static class PresetService
    {
        public static IReadOnlyList<OptimizationPreset> GetPresets() => new List<OptimizationPreset>
        {
            new()
            {
                Id          = "preset_gaming",
                Name        = Strings.Preset_Gaming_Name,
                Description = Strings.Preset_Gaming_Desc,
                OptimizationIds = new[]
                {
                    "gaming_highperf", "gaming_gamedvr", "gaming_fullscreen",
                    "gaming_gpu_schedule", "gaming_kill_processes", "net_ping"
                }
            },
            new()
            {
                Id          = "preset_perf",
                Name        = Strings.Preset_Perf_Name,
                Description = Strings.Preset_Perf_Desc,
                OptimizationIds = new[]
                {
                    "gaming_highperf", "oldpc_startup", "oldpc_animations", "oldpc_ram",
                    "oldpc_temp", "clean_disk", "clean_recycle", "clean_prefetch"
                }
            },
            new()
            {
                Id          = "preset_privacy",
                Name        = Strings.Preset_Privacy_Name,
                Description = Strings.Preset_Privacy_Desc,
                OptimizationIds = new[]
                {
                    "priv_telemetry", "priv_advertising_id", "priv_activity_history",
                    "priv_location", "priv_tailored", "priv_app_launch_tracking", "priv_feedback"
                }
            },
            new()
            {
                Id          = "preset_balanced",
                Name        = Strings.Preset_Balanced_Name,
                Description = Strings.Preset_Balanced_Desc,
                OptimizationIds = new[]
                {
                    "clean_disk", "clean_recycle", "oldpc_startup", "oldpc_animations",
                    "oldpc_temp", "priv_telemetry", "net_flush_dns"
                }
            },
        };

        /// <summary>
        /// Construit un dictionnaire { ID → OptimizationItem } à partir de tous les modules.
        /// Chaque appel recrée des instances fraîches (actions liées).
        /// </summary>
        public static Dictionary<string, OptimizationItem> GetAllOptimizationsById()
        {
            var all = new Dictionary<string, OptimizationItem>(StringComparer.OrdinalIgnoreCase);

            void Add(IEnumerable<OptimizationItem> items)
            {
                foreach (var it in items)
                    all[it.Id] = it;
            }

            Add(GamingOptimizations.GetOptimizations());
            Add(OldPcOptimizations.GetOptimizations());
            Add(CleaningOptimizations.GetOptimizations());
            Add(NetworkOptimizations.GetOptimizations());
            Add(StreamingOptimizations.GetOptimizations());
            Add(PrivacyOptimizations.GetOptimizations());

            return all;
        }

        /// <summary>Résout les optims d'un profil en instances exécutables (ignore les ID absents).</summary>
        public static List<OptimizationItem> ResolveItems(OptimizationPreset preset)
        {
            var all = GetAllOptimizationsById();
            var list = new List<OptimizationItem>();
            foreach (var id in preset.OptimizationIds)
                if (all.TryGetValue(id, out var item))
                    list.Add(item);
            preset.ResolvedCount = list.Count;
            return list;
        }
    }
}
