using InfoZen.Models;
using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;

namespace InfoZen.Services
{
    /// <summary>
    /// Module Réseau – optimisations et diagnostic de la connexion internet.
    /// </summary>
    public static class NetworkOptimizations
    {
        public static List<OptimizationItem> GetOptimizations()
        {
            var items = new List<OptimizationItem>
            {
            new OptimizationItem
            {
                Id          = "net_tcpip",
                Name        = "Optimisation TCP/IP",
                Description = "Ajuste les paramètres de la pile réseau Windows : autotuning, ECN, timestamps pour meilleures performances.",
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string script = @"
                        netsh int tcp set global autotuninglevel=normal
                        netsh int tcp set global ecncapability=enabled
                        netsh int tcp set global timestamps=disabled
                        netsh int tcp set global rss=enabled
                        netsh int tcp set global chimney=enabled
                        Write-Output '✅ Paramètres TCP/IP optimisés.'
                    ";
                    return (await SystemService.RunPowerShellAsync(script, asAdmin: true)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "net_reset",
                Name        = "Réinitialisation Réseau",
                Description = "Remet à zéro tous les paramètres réseau (netsh winsock reset + int ip reset). Redémarrage requis.",
                Category    = "Réseau",
                RequiresAdmin = true,
                RequiresReboot = true,
                Action = async () =>
                {
                    string backup = await BackupNetworkStateAsync();
                    var r1 = await SystemService.RunCmdAsync("netsh winsock reset");
                    var r2 = await SystemService.RunCmdAsync("netsh int ip reset");
                    var r3 = await SystemService.RunCmdAsync("netsh int ipv4 reset");
                    var r4 = await SystemService.RunCmdAsync("netsh int ipv6 reset");
                    return $"{backup}\n✅ Réseau réinitialisé.\n{r1.DisplayMessage}\n{r2.DisplayMessage}\n{r3.DisplayMessage}\n{r4.DisplayMessage}\n⚠️ Redémarrage requis pour appliquer les changements.";
                }
            },
            new OptimizationItem
            {
                Id          = "net_dns_fast",
                Name        = "DNS Rapides (Cloudflare / Google)",
                Description = "Benchmarke plusieurs profils DNS puis applique automatiquement le plus rapide.",
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string script = @"
                        $profiles = @(
                            @{ Name = 'Cloudflare+Google'; Servers = @('1.1.1.1', '8.8.8.8') },
                            @{ Name = 'Google+Cloudflare'; Servers = @('8.8.8.8', '1.1.1.1') },
                            @{ Name = 'Quad9+Cloudflare'; Servers = @('9.9.9.9', '1.1.1.1') }
                        )
                        $domains = @('github.com', 'microsoft.com', 'cloudflare.com')
                        $best = $null

                        foreach ($profile in $profiles) {
                            $samples = @()
                            foreach ($d in $domains) {
                                try {
                                    $sw = [System.Diagnostics.Stopwatch]::StartNew()
                                    Resolve-DnsName -Name $d -Server $profile.Servers[0] -DnsOnly -ErrorAction Stop | Out-Null
                                    $sw.Stop()
                                    $samples += $sw.Elapsed.TotalMilliseconds
                                } catch {
                                    $samples += 1000
                                }
                            }
                            $avg = [math]::Round(($samples | Measure-Object -Average).Average, 1)
                            if (-not $best -or $avg -lt $best.Avg) {
                                $best = [PSCustomObject]@{
                                    Name = $profile.Name
                                    Servers = $profile.Servers
                                    Avg = $avg
                                }
                            }
                        }

                        $adapters = Get-NetAdapter | Where-Object { $_.Status -eq 'Up' }
                        if (-not $adapters) {
                            Write-Output '❌ Aucun adaptateur réseau actif détecté.'
                            exit 1
                        }

                        foreach ($adapter in $adapters) {
                            Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses $best.Servers -ErrorAction Stop
                        }
                        Write-Output ""✅ DNS auto-optimisés : $($best.Name) [$($best.Servers -join ', ')] — latence DNS moyenne $($best.Avg) ms""
                    ";
                    return (await SystemService.RunPowerShellAsync(script, asAdmin: true)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "net_ping",
                Name        = "Optimisation Ping",
                Description = "Réduit la latence réseau en ajustant les timers TCP et désactivant les délais d'ACK.",
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string script = @"
                        # Désactiver Nagle + ACK delay sur toutes les interfaces
                        $ifPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces'
                        Get-ChildItem $ifPath | ForEach-Object {
                            Set-ItemProperty -Path $_.PSPath -Name 'TcpAckFrequency' -Value 1    -Type DWord -ErrorAction SilentlyContinue
                            Set-ItemProperty -Path $_.PSPath -Name 'TCPNoDelay'       -Value 1    -Type DWord -ErrorAction SilentlyContinue
                            Set-ItemProperty -Path $_.PSPath -Name 'TcpDelAckTicks'   -Value 0    -Type DWord -ErrorAction SilentlyContinue
                        }
                        Write-Output '✅ Latence optimisée : Nagle désactivé, ACK delay = 0'
                    ";
                    return (await SystemService.RunPowerShellAsync(script, asAdmin: true)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "net_telemetry_block",
                Name        = "Blocage Télémétrie Microsoft",
                Description = "Bloque les connexions aux serveurs de tracking et télémétrie Microsoft via le fichier hosts.",
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string[] domains = {
                        "telemetry.microsoft.com", "vortex.data.microsoft.com",
                        "settings-win.data.microsoft.com", "watson.telemetry.microsoft.com",
                        "oca.telemetry.microsoft.com", "sqm.telemetry.microsoft.com"
                    };
                    string hostsPath = @"C:\Windows\System32\drivers\etc\hosts";
                    try
                    {
                        string backup = BackupHostsFile(hostsPath);
                        string existing = File.ReadAllText(hostsPath);
                        var sb = new StringBuilder(existing);
                        if (!existing.Contains("# InfoZen Telemetry Block"))
                            sb.AppendLine("\n# InfoZen Telemetry Block");
                        int added = 0;
                        foreach (var d in domains)
                        {
                            string entry = $"0.0.0.0 {d}";
                            if (!existing.Contains(d)) { sb.AppendLine(entry); added++; }
                        }
                        File.WriteAllText(hostsPath, sb.ToString());
                        return $"{backup}\n✅ {added} domaines de télémétrie bloqués dans hosts.";
                    }
                    catch (Exception ex) { return $"❌ Erreur : {ex.Message}"; }
                }
            },
            new OptimizationItem
            {
                Id          = "net_flush_dns",
                Name        = "Flush DNS",
                Description = "Vide le cache DNS local (ipconfig /flushdns) pour résoudre des problèmes de navigation.",
                Category    = "Réseau",
                Action = async () => (await SystemService.RunCmdAsync("ipconfig /flushdns")).DisplayMessage
            },
            new OptimizationItem
            {
                Id          = "net_reset_firewall",
                Name        = "Reset Pare-feu Windows",
                Description = "Restaure la configuration par défaut du pare-feu Windows (toutes les règles personnalisées seront supprimées).",
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string backup = await BackupFirewallRulesAsync();
                    var result = await SystemService.RunCmdAsync("netsh advfirewall reset");
                    return $"{backup}\n✅ Pare-feu réinitialisé.\n{result.DisplayMessage}";
                }
            },
            new OptimizationItem
            {
                Id          = "net_speed_test",
                Name        = "Test Vitesse Internet",
                Description = "Mesure la vitesse download (multi-runs) + ping, jitter et perte de paquets.",
                Category    = "Réseau",
                Action = async () =>
                {
                    return await MeasureDownloadSpeedAsync();
                }
            },
            new OptimizationItem
            {
                Id          = "net_info",
                Name        = "Informations Réseau",
                Description = "Affiche IP locale, DNS, passerelle, adresse MAC et état des interfaces réseau actives.",
                Category    = "Réseau",
                Action = async () =>
                {
                    return await GetNetworkInfoAsync();
                }
            }
            };

            OptimizationProfileService.ApplyMetadata(items);
            return items;
        }

        // ─────────────────────────────── HELPERS ────────────────────────────────

        private static async Task<string> MeasureDownloadSpeedAsync()
        {
            try
            {
                var (avgLatency, jitter, loss) = await MeasurePingQualityAsync("1.1.1.1");

                // Trois runs pour limiter la variance CDN/réseau.
                const string url = "https://speed.cloudflare.com/__down?bytes=3000000";
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(30);

                var speedSamples = new List<double>();
                var durationSamples = new List<double>();

                for (int i = 0; i < 3; i++)
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    var data = await client.GetByteArrayAsync(url);
                    sw.Stop();

                    double mb = data.Length / (1024.0 * 1024.0);
                    double seconds = Math.Max(sw.Elapsed.TotalSeconds, 0.001);
                    speedSamples.Add((mb / seconds) * 8); // Mbps
                    durationSamples.Add(seconds);
                }

                var ordered = speedSamples.OrderBy(v => v).ToList();
                double medianMbps = ordered[ordered.Count / 2];
                double avgMbps = speedSamples.Average();
                double bestMbps = speedSamples.Max();

                return
                    "✅ Test terminé\n" +
                    $"📥 Download médian : {medianMbps:F1} Mbps\n" +
                    $"📈 Download moyen   : {avgMbps:F1} Mbps (max {bestMbps:F1})\n" +
                    $"📶 Ping moyen       : {(avgLatency.HasValue ? $"{avgLatency.Value:F1} ms" : "n/a")}\n" +
                    $"〰️ Jitter           : {(jitter.HasValue ? $"{jitter.Value:F1} ms" : "n/a")}\n" +
                    $"📉 Perte paquets    : {(loss.HasValue ? $"{loss.Value:F1}%" : "n/a")}";
            }
            catch (Exception ex)
            {
                return $"❌ Test échoué : {ex.Message}";
            }
        }

        private static async Task<(double? AvgLatencyMs, double? JitterMs, double? LossPercent)> MeasurePingQualityAsync(string host)
        {
            try
            {
                using var ping = new Ping();
                var success = new List<long>();
                int attempts = 8;
                int failed = 0;

                for (int i = 0; i < attempts; i++)
                {
                    var reply = await ping.SendPingAsync(host, 1200);
                    if (reply.Status == IPStatus.Success)
                        success.Add(reply.RoundtripTime);
                    else
                        failed++;
                }

                if (success.Count == 0)
                    return (null, null, 100);

                double avg = success.Average();
                double jitter = 0;
                if (success.Count > 1)
                {
                    var deltas = new List<double>();
                    for (int i = 1; i < success.Count; i++)
                        deltas.Add(Math.Abs(success[i] - success[i - 1]));
                    jitter = deltas.Average();
                }

                double loss = ((double)failed / attempts) * 100.0;
                return (avg, jitter, loss);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Mesure ping/jitter indisponible : {ex.Message}", "Réseau");
                return (null, null, null);
            }
        }

        private static string EnsureBackupDirectory()
        {
            string backupDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "InfoZen",
                "Backups");
            Directory.CreateDirectory(backupDir);
            return backupDir;
        }

        private static async Task<string> BackupNetworkStateAsync()
        {
            try
            {
                string backupDir = EnsureBackupDirectory();
                string filePath = Path.Combine(backupDir, $"network_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

                var winsock = await SystemService.RunCmdAsync("netsh winsock show catalog");
                var ipv4 = await SystemService.RunCmdAsync("netsh interface ipv4 show config");
                var ipv6 = await SystemService.RunCmdAsync("netsh interface ipv6 show interfaces");
                var ipconfig = await SystemService.RunCmdAsync("ipconfig /all");

                var sb = new StringBuilder();
                sb.AppendLine("=== SAUVEGARDE RÉSEAU INFOZEN ===");
                sb.AppendLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine();
                sb.AppendLine("=== NETSH WINSOCK SHOW CATALOG ===");
                sb.AppendLine(winsock.DisplayMessage);
                sb.AppendLine();
                sb.AppendLine("=== NETSH INTERFACE IPV4 SHOW CONFIG ===");
                sb.AppendLine(ipv4.DisplayMessage);
                sb.AppendLine();
                sb.AppendLine("=== NETSH INTERFACE IPV6 SHOW INTERFACES ===");
                sb.AppendLine(ipv6.DisplayMessage);
                sb.AppendLine();
                sb.AppendLine("=== IPCONFIG /ALL ===");
                sb.AppendLine(ipconfig.DisplayMessage);

                File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
                return $"📦 Sauvegarde réseau : {filePath}";
            }
            catch (Exception ex)
            {
                return $"⚠ Sauvegarde réseau non créée : {ex.Message}";
            }
        }

        private static async Task<string> BackupFirewallRulesAsync()
        {
            try
            {
                string backupDir = EnsureBackupDirectory();
                string filePath = Path.Combine(backupDir, $"firewall_{DateTime.Now:yyyyMMdd_HHmmss}.wfw");
                var exportResult = await SystemService.RunCmdAsync($"netsh advfirewall export \"{filePath}\"");

                return exportResult.Success
                    ? $"📦 Sauvegarde pare-feu : {filePath}"
                    : $"⚠ Sauvegarde pare-feu non créée : {exportResult.DisplayMessage}";
            }
            catch (Exception ex)
            {
                return $"⚠ Sauvegarde pare-feu non créée : {ex.Message}";
            }
        }

        private static string BackupHostsFile(string hostsPath)
        {
            try
            {
                string backupDir = EnsureBackupDirectory();
                string backupPath = Path.Combine(backupDir, $"hosts_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
                File.Copy(hostsPath, backupPath, overwrite: true);
                return $"📦 Sauvegarde hosts : {backupPath}";
            }
            catch (Exception ex)
            {
                return $"⚠ Sauvegarde hosts non créée : {ex.Message}";
            }
        }

        private static async Task<string> GetNetworkInfoAsync()
        {
            await Task.Yield();
            var sb = new StringBuilder();
            try
            {
                var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n => n.OperationalStatus == OperationalStatus.Up
                             && n.NetworkInterfaceType != NetworkInterfaceType.Loopback);

                foreach (var ni in interfaces)
                {
                    var ipProps = ni.GetIPProperties();
                    sb.AppendLine($"┌─ {ni.Name} ({ni.NetworkInterfaceType})");
                    sb.AppendLine($"│  MAC    : {ni.GetPhysicalAddress()}");

                    foreach (var ua in ipProps.UnicastAddresses)
                    {
                        if (ua.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                            sb.AppendLine($"│  IP     : {ua.Address}");
                    }
                    foreach (var gw in ipProps.GatewayAddresses)
                        sb.AppendLine($"│  GW     : {gw.Address}");
                    foreach (var dns in ipProps.DnsAddresses)
                        sb.AppendLine($"│  DNS    : {dns}");
                    sb.AppendLine("└─────────────────────────────");
                }
            }
            catch (Exception ex) { sb.AppendLine($"❌ Erreur : {ex.Message}"); }
            return sb.ToString();
        }
    }
}
