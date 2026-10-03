using Klyr.Models;
using Klyr.Resources;
using Microsoft.Win32;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;

namespace Klyr.Services
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
                Name        = Strings.Optim_net_tcpip_Name,
                Description = Strings.Optim_net_tcpip_Desc,
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    string script = @"
                        netsh int tcp set global autotuninglevel=normal
                        netsh int tcp set global ecncapability=enabled
                        netsh int tcp set global timestamps=disabled
                        netsh int tcp set global rss=enabled
                        netsh int tcp set global chimney=enabled
                        Write-Output 'OK_TCP'
                    ";
                    var psResult = await SystemService.RunPowerShellAsync(script, asAdmin: true);
                    return psResult.Output.Contains("OK_TCP")
                        ? Strings.Result_TcpOptimized
                        : psResult.DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "net_reset",
                Name        = Strings.Optim_net_reset_Name,
                Description = Strings.Optim_net_reset_Desc,
                Category    = "Réseau",
                RequiresAdmin = true,
                RequiresReboot = true,
                Action = async ct =>
                {
                    string backup = await BackupNetworkStateAsync();
                    var r1 = await SystemService.RunCmdAsync("netsh winsock reset");
                    var r2 = await SystemService.RunCmdAsync("netsh int ip reset");
                    var r3 = await SystemService.RunCmdAsync("netsh int ipv4 reset");
                    var r4 = await SystemService.RunCmdAsync("netsh int ipv6 reset");
                    return $"{backup}\n{Strings.Result_NetReset}\n{r1.DisplayMessage}\n{r2.DisplayMessage}\n{r3.DisplayMessage}\n{r4.DisplayMessage}";
                }
            },
            new OptimizationItem
            {
                Id          = "net_dns_fast",
                Name        = Strings.Optim_net_dns_fast_Name,
                Description = Strings.Optim_net_dns_fast_Desc,
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    string script = @"
                        if ((Get-CimInstance Win32_ComputerSystem).PartOfDomain) {
                            Write-Output 'DOMAIN_JOINED'
                            exit 1
                        }

                        $profiles = @(
                            @{ Name = 'Cloudflare+Google'; Servers = @('1.1.1.1', '8.8.8.8') },
                            @{ Name = 'Google+Cloudflare'; Servers = @('8.8.8.8', '1.1.1.1') },
                            @{ Name = 'Quad9+Cloudflare'; Servers = @('9.9.9.9', '1.1.1.1') }
                        )
                        $domains = @('github.com', 'microsoft.com', 'cloudflare.com')
                        $best = $null

                        foreach ($profile in $profiles) {
                            $samples = @()
                            foreach ($server in $profile.Servers) {
                                foreach ($d in $domains) {
                                    try {
                                        $sw = [System.Diagnostics.Stopwatch]::StartNew()
                                        Resolve-DnsName -Name $d -Server $server -DnsOnly -ErrorAction Stop | Out-Null
                                        $sw.Stop()
                                        $samples += $sw.Elapsed.TotalMilliseconds
                                    } catch {
                                        $samples += 1000
                                    }
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

                        # Cartes physiques uniquement : les adaptateurs VPN / virtuels gardent leur DNS
                        $adapters = @(Get-NetAdapter -Physical | Where-Object { $_.Status -eq 'Up' })
                        if ($adapters.Count -eq 0) {
                            Write-Output 'NO_ADAPTER'
                            exit 1
                        }

                        # Sauvegarde de la configuration actuelle AVANT toute modification
                        $backupDir = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Klyr\Backups'
                        $backupFile = Join-Path $backupDir ('dns_{0}.json' -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
                        try {
                            New-Item -ItemType Directory -Path $backupDir -Force -ErrorAction Stop | Out-Null
                            $snapshot = foreach ($adapter in $adapters) {
                                $regPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\{0}' -f $adapter.InterfaceGuid
                                $nameServer = (Get-ItemProperty -Path $regPath -Name NameServer -ErrorAction SilentlyContinue).NameServer
                                [PSCustomObject]@{
                                    InterfaceGuid   = $adapter.InterfaceGuid
                                    InterfaceAlias  = $adapter.Name
                                    Static          = -not [string]::IsNullOrWhiteSpace($nameServer)
                                    ServerAddresses = @((Get-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -AddressFamily IPv4 -ErrorAction Stop).ServerAddresses)
                                }
                            }
                            ConvertTo-Json -InputObject @($snapshot) -Depth 4 | Set-Content -Path $backupFile -Encoding UTF8 -ErrorAction Stop
                        } catch {
                            Write-Output 'BACKUP_FAILED'
                            exit 1
                        }

                        foreach ($adapter in $adapters) {
                            Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses $best.Servers -ErrorAction Stop
                        }
                        Write-Output ""DNS auto-optimisés : $($best.Name) [$($best.Servers -join ', ')] — latence DNS moyenne $($best.Avg) ms. Sauvegarde : $backupFile""
                    ";
                    var dnsResult = await SystemService.RunPowerShellAsync(script, asAdmin: true);
                    if (dnsResult.Output.Contains("DOMAIN_JOINED")) return Strings.Result_DnsDomainJoined;
                    if (dnsResult.Output.Contains("BACKUP_FAILED")) return Strings.Result_DnsBackupFailed;
                    return dnsResult.Output.Contains("NO_ADAPTER")
                        ? Strings.Result_NoActiveAdapter
                        : dnsResult.DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "net_dns_restore",
                Name        = Strings.Optim_net_dns_restore_Name,
                Description = Strings.Optim_net_dns_restore_Desc,
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    string script = @"
                        $backupDir = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'Klyr\Backups'
                        $files = @(Get-ChildItem -Path $backupDir -Filter 'dns_*.json' -ErrorAction SilentlyContinue | Sort-Object LastWriteTime)
                        if ($files.Count -eq 0) {
                            Write-Output 'NO_BACKUP'
                            exit 1
                        }

                        # La plus ancienne sauvegarde non restaurée = la configuration d'origine
                        $entries = @(Get-Content -Path $files[0].FullName -Raw -Encoding UTF8 | ConvertFrom-Json)
                        $restored = 0
                        foreach ($entry in $entries) {
                            $adapter = Get-NetAdapter | Where-Object { $_.InterfaceGuid -eq $entry.InterfaceGuid } | Select-Object -First 1
                            if (-not $adapter) { continue }
                            if ($entry.Static -and @($entry.ServerAddresses).Count -gt 0) {
                                Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ServerAddresses @($entry.ServerAddresses) -ErrorAction Stop
                            } else {
                                Set-DnsClientServerAddress -InterfaceIndex $adapter.InterfaceIndex -ResetServerAddresses -ErrorAction Stop
                            }
                            $restored++
                        }

                        $files | ForEach-Object { Rename-Item -LiteralPath $_.FullName -NewName ($_.Name + '.restored') -Force -ErrorAction SilentlyContinue }
                        Write-Output ""DNS d'origine restaurés sur $restored adaptateur(s).""
                    ";
                    var restoreResult = await SystemService.RunPowerShellAsync(script, asAdmin: true);
                    return restoreResult.Output.Contains("NO_BACKUP")
                        ? Strings.Result_DnsNoBackup
                        : restoreResult.DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "net_ping",
                Name        = Strings.Optim_net_ping_Name,
                Description = Strings.Optim_net_ping_Desc,
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    string script = @"
                        # Désactiver Nagle + ACK delay sur toutes les interfaces
                        $ifPath = 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces'
                        Get-ChildItem $ifPath | ForEach-Object {
                            Set-ItemProperty -Path $_.PSPath -Name 'TcpAckFrequency' -Value 1    -Type DWord -ErrorAction SilentlyContinue
                            Set-ItemProperty -Path $_.PSPath -Name 'TCPNoDelay'       -Value 1    -Type DWord -ErrorAction SilentlyContinue
                            Set-ItemProperty -Path $_.PSPath -Name 'TcpDelAckTicks'   -Value 0    -Type DWord -ErrorAction SilentlyContinue
                        }
                        Write-Output 'OK_PING'
                    ";
                    var pingResult = await SystemService.RunPowerShellAsync(script, asAdmin: true);
                    return pingResult.Output.Contains("OK_PING")
                        ? Strings.Result_PingOptimized
                        : pingResult.DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "net_telemetry_block",
                Name        = Strings.Optim_net_telemetry_block_Name,
                Description = Strings.Optim_net_telemetry_block_Desc,
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async ct =>
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
                        if (!existing.Contains("# Klyr Telemetry Block"))
                            sb.AppendLine("\n# Klyr Telemetry Block");
                        int added = 0;
                        foreach (var d in domains)
                        {
                            string entry = $"0.0.0.0 {d}";
                            if (!existing.Contains(d)) { sb.AppendLine(entry); added++; }
                        }
                        File.WriteAllText(hostsPath, sb.ToString());
                        return $"{backup}\n{string.Format(Strings.Result_TelemetryBlocked, added)}";
                    }
                    catch (Exception ex) { return $"Erreur : {ex.Message}"; }
                }
            },
            new OptimizationItem
            {
                Id          = "net_flush_dns",
                Name        = Strings.Optim_net_flush_dns_Name,
                Description = Strings.Optim_net_flush_dns_Desc,
                Category    = "Réseau",
                Action = async ct => (await SystemService.RunCmdAsync("ipconfig /flushdns")).DisplayMessage
            },
            new OptimizationItem
            {
                Id          = "net_reset_firewall",
                Name        = Strings.Optim_net_reset_firewall_Name,
                Description = Strings.Optim_net_reset_firewall_Desc,
                Category    = "Réseau",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    string backup = await BackupFirewallRulesAsync();
                    var result = await SystemService.RunCmdAsync("netsh advfirewall reset");
                    return $"{backup}\n{Strings.Result_FirewallReset}\n{result.DisplayMessage}";
                }
            },
            new OptimizationItem
            {
                Id          = "net_speed_test",
                Name        = Strings.Optim_net_speed_test_Name,
                Description = Strings.Optim_net_speed_test_Desc,
                Category    = "Réseau",
                Action = async ct =>
                {
                    return await MeasureDownloadSpeedAsync();
                }
            },
            new OptimizationItem
            {
                Id          = "net_info",
                Name        = Strings.Optim_net_info_Name,
                Description = Strings.Optim_net_info_Desc,
                Category    = "Réseau",
                Action = async ct =>
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
                    "Test terminé\n" +
                    $"Download médian : {medianMbps:F1} Mbps\n" +
                    $"Download moyen  : {avgMbps:F1} Mbps (max {bestMbps:F1})\n" +
                    $"Ping moyen      : {(avgLatency.HasValue ? $"{avgLatency.Value:F1} ms" : "n/a")}\n" +
                    $"Jitter          : {(jitter.HasValue ? $"{jitter.Value:F1} ms" : "n/a")}\n" +
                    $"Perte paquets   : {(loss.HasValue ? $"{loss.Value:F1}%" : "n/a")}";
            }
            catch (Exception ex)
            {
                return $"Test échoué : {ex.Message}";
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
                "Klyr",
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
                return $"Backup : Sauvegarde réseau : {filePath}";
            }
            catch (Exception ex)
            {
                return $"Sauvegarde réseau non créée : {ex.Message}";
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
                    ? $"Backup : Sauvegarde pare-feu : {filePath}"
                    : $"Sauvegarde pare-feu non créée : {exportResult.DisplayMessage}";
            }
            catch (Exception ex)
            {
                return $"Sauvegarde pare-feu non créée : {ex.Message}";
            }
        }

        private static string BackupHostsFile(string hostsPath)
        {
            try
            {
                string backupDir = EnsureBackupDirectory();
                string backupPath = Path.Combine(backupDir, $"hosts_{DateTime.Now:yyyyMMdd_HHmmss}.bak");
                File.Copy(hostsPath, backupPath, overwrite: true);
                return $"Backup : Sauvegarde hosts : {backupPath}";
            }
            catch (Exception ex)
            {
                return $"Sauvegarde hosts non créée : {ex.Message}";
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
            catch (Exception ex) { sb.AppendLine($"Erreur : {ex.Message}"); }
            return sb.ToString();
        }
    }
}
