using System.Net.NetworkInformation;
using System.Text;
using Klyr.Models;

namespace Klyr.Services
{
    public sealed class OptimizationBenchmarkSnapshot
    {
        public float CpuUsage { get; init; }
        public float RamUsedGb { get; init; }
        public long DiskFreeGb { get; init; }
        public double? NetworkLatencyMs { get; init; }
    }

    /// <summary>
    /// Captures légères avant/après pour objectiver les gains des optimisations majeures.
    /// </summary>
    public static class OptimizationBenchmarkService
    {
        public static async Task<OptimizationBenchmarkSnapshot> CaptureAsync(OptimizationItem item)
        {
            var info = SystemService.GetSystemInfo();
            float cpu = SystemService.GetCpuUsage();
            double? latency = null;

            if (item.Category == "Réseau" || item.Id.StartsWith("net_", StringComparison.OrdinalIgnoreCase))
                latency = await MeasureLatencyAsync();

            return new OptimizationBenchmarkSnapshot
            {
                CpuUsage = cpu,
                RamUsedGb = info.RamUsedGb,
                DiskFreeGb = info.DiskFreeGb,
                NetworkLatencyMs = latency
            };
        }

        public static string BuildSummary(
            OptimizationItem item,
            OptimizationBenchmarkSnapshot before,
            OptimizationBenchmarkSnapshot after,
            TimeSpan elapsed)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Benchmark: {item.Name}");
            sb.AppendLine($"Durée: {elapsed.TotalSeconds:F1}s");
            sb.AppendLine($"CPU: {before.CpuUsage:F0}% -> {after.CpuUsage:F0}% ({FormatDelta(after.CpuUsage - before.CpuUsage, "pp")})");
            sb.AppendLine($"RAM utilisée: {before.RamUsedGb:F2} Go -> {after.RamUsedGb:F2} Go ({FormatDelta(after.RamUsedGb - before.RamUsedGb, "Go")})");
            sb.AppendLine($"Disque libre C: {before.DiskFreeGb} Go -> {after.DiskFreeGb} Go ({FormatDelta(after.DiskFreeGb - before.DiskFreeGb, "Go")})");

            if (before.NetworkLatencyMs.HasValue && after.NetworkLatencyMs.HasValue)
            {
                sb.AppendLine(
                    $"Latence réseau: {before.NetworkLatencyMs.Value:F1}ms -> {after.NetworkLatencyMs.Value:F1}ms " +
                    $"({FormatDelta(after.NetworkLatencyMs.Value - before.NetworkLatencyMs.Value, "ms")})");
            }

            return sb.ToString().TrimEnd();
        }

        private static async Task<double?> MeasureLatencyAsync()
        {
            try
            {
                using var ping = new Ping();
                var samples = new List<long>();

                for (int i = 0; i < 3; i++)
                {
                    var reply = await ping.SendPingAsync("1.1.1.1", 1200);
                    if (reply.Status == IPStatus.Success)
                        samples.Add(reply.RoundtripTime);
                }

                if (samples.Count == 0) return null;
                return samples.Average();
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Mesure de latence benchmark indisponible : {ex.Message}", "Benchmark");
                return null;
            }
        }

        private static string FormatDelta(double value, string unit)
            => value switch
            {
                > 0 => $"+{value:F2} {unit}",
                < 0 => $"{value:F2} {unit}",
                _ => $"0 {unit}"
            };
    }
}
