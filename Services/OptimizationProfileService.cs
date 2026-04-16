using InfoZen.Models;

namespace InfoZen.Services
{
    /// <summary>
    /// Applique les profils OPTIMUS: confiance, type d'optimisation, mode avancé et benchmark.
    /// </summary>
    public static class OptimizationProfileService
    {
        private sealed record OptimizationProfile(
            OptimizationPurpose Purpose,
            int ConfidenceScore,
            bool IsAdvanced = false,
            bool IsBenchmarkCandidate = false);

        private static readonly Dictionary<string, OptimizationProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
        {
            // Gaming
            ["gaming_highperf"] = new(OptimizationPurpose.Performance, 82, IsBenchmarkCandidate: true),
            ["gaming_gamedvr"] = new(OptimizationPurpose.Performance, 75, IsBenchmarkCandidate: true),
            ["gaming_fullscreen"] = new(OptimizationPurpose.Troubleshooting, 45, IsAdvanced: true),
            ["gaming_priority"] = new(OptimizationPurpose.Troubleshooting, 35, IsAdvanced: true),
            ["gaming_network_latency"] = new(OptimizationPurpose.Troubleshooting, 30, IsAdvanced: true),
            ["gaming_directx"] = new(OptimizationPurpose.Troubleshooting, 42, IsAdvanced: true),
            ["gaming_kill_processes"] = new(OptimizationPurpose.Performance, 70),
            ["gaming_fps_unlock"] = new(OptimizationPurpose.Troubleshooting, 20, IsAdvanced: true),
            ["gaming_gpu_schedule"] = new(OptimizationPurpose.Troubleshooting, 42, IsAdvanced: true),

            // Vieux PC
            ["oldpc_startup"] = new(OptimizationPurpose.Performance, 80, IsBenchmarkCandidate: true),
            ["oldpc_animations"] = new(OptimizationPurpose.Performance, 68),
            ["oldpc_registry"] = new(OptimizationPurpose.SecurityPrivacy, 15, IsAdvanced: true),
            ["oldpc_ram"] = new(OptimizationPurpose.Troubleshooting, 40, IsAdvanced: true),
            ["oldpc_temp"] = new(OptimizationPurpose.Maintenance, 78, IsBenchmarkCandidate: true),
            ["oldpc_defrag"] = new(OptimizationPurpose.Performance, 85, IsBenchmarkCandidate: true),
            ["oldpc_theme"] = new(OptimizationPurpose.Performance, 35),
            ["oldpc_telemetry"] = new(OptimizationPurpose.SecurityPrivacy, 28, IsAdvanced: true),

            // Nettoyage
            ["clean_disk"] = new(OptimizationPurpose.Maintenance, 72),
            ["clean_recycle"] = new(OptimizationPurpose.Maintenance, 20),
            ["clean_logs"] = new(OptimizationPurpose.Maintenance, 22, IsAdvanced: true),
            ["clean_prefetch"] = new(OptimizationPurpose.Troubleshooting, 10, IsAdvanced: true),
            ["clean_bloatware"] = new(OptimizationPurpose.Maintenance, 58),
            ["clean_repair"] = new(OptimizationPurpose.Maintenance, 76, IsBenchmarkCandidate: true),
            ["clean_dns_cache"] = new(OptimizationPurpose.Troubleshooting, 25),
            ["clean_antivirus"] = new(OptimizationPurpose.SecurityPrivacy, 30),

            // Réseau
            ["net_tcpip"] = new(OptimizationPurpose.Troubleshooting, 35, IsAdvanced: true),
            ["net_reset"] = new(OptimizationPurpose.Troubleshooting, 60),
            ["net_dns_fast"] = new(OptimizationPurpose.Performance, 74, IsBenchmarkCandidate: true),
            ["net_ping"] = new(OptimizationPurpose.Troubleshooting, 30, IsAdvanced: true),
            ["net_telemetry_block"] = new(OptimizationPurpose.SecurityPrivacy, 20, IsAdvanced: true),
            ["net_flush_dns"] = new(OptimizationPurpose.Troubleshooting, 25),
            ["net_reset_firewall"] = new(OptimizationPurpose.Troubleshooting, 50),
            ["net_speed_test"] = new(OptimizationPurpose.Maintenance, 0),
            ["net_info"] = new(OptimizationPurpose.Maintenance, 0),
        };

        public static void ApplyMetadata(IEnumerable<OptimizationItem> items)
        {
            foreach (var item in items)
            {
                if (!Profiles.TryGetValue(item.Id, out var profile))
                    continue;

                item.Purpose = profile.Purpose;
                item.ConfidenceScore = profile.ConfidenceScore;
                item.IsAdvanced = profile.IsAdvanced;
                item.IsBenchmarkCandidate = profile.IsBenchmarkCandidate;
            }
        }
    }
}
