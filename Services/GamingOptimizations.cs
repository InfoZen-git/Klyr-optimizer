using System.Windows;
using Klyr.Models;
using Klyr.Resources;
using Microsoft.Win32;

namespace Klyr.Services
{
    /// <summary>
    /// Module Gaming / FPS – toutes les optimisations jeu vidéo.
    /// </summary>
    public static class GamingOptimizations
    {
        public static List<OptimizationItem> GetOptimizations()
        {
            var items = new List<OptimizationItem>
            {
            new OptimizationItem
            {
                Id          = "gaming_highperf",
                Name        = Strings.Optim_gaming_highperf_Name,
                Description = Strings.Optim_gaming_highperf_Desc,
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    var result = await SystemService.RunCmdAsync("powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                    return result.Success
                        ? Strings.Result_HighPerfActive
                        : result.DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_gamedvr",
                Name        = Strings.Optim_gaming_gamedvr_Name,
                Description = Strings.Optim_gaming_gamedvr_Desc,
                Category    = "Gaming",
                Action = async ct =>
                {
                    await Task.Yield();
                    string r1 = SystemService.SetRegistryCurrentUser(
                        @"System\GameConfigStore", "GameDVR_Enabled", 0, RegistryValueKind.DWord);
                    string r2 = SystemService.SetRegistryCurrentUser(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0, RegistryValueKind.DWord);
                    return $"{r1}\n{r2}";
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_fullscreen",
                Name        = Strings.Optim_gaming_fullscreen_Name,
                Description = Strings.Optim_gaming_fullscreen_Desc,
                Category    = "Gaming",
                Action = async ct =>
                {
                    await Task.Yield();
                    return SystemService.SetRegistryCurrentUser(
                        @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_priority",
                Name        = Strings.Optim_gaming_priority_Name,
                Description = Strings.Optim_gaming_priority_Desc,
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    await Task.Yield();
                    string r1 = SystemService.SetRegistryValue(
                        @"SYSTEM\CurrentControlSet\Control\PriorityControl",
                        "Win32PrioritySeparation", 38, RegistryValueKind.DWord);
                    return r1;
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_network_latency",
                Name        = Strings.Optim_gaming_network_latency_Name,
                Description = Strings.Optim_gaming_network_latency_Desc,
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    string script = @"
                        # Désactiver l'algorithme de Nagle
                        $adapters = Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces'
                        foreach ($adapter in $adapters) {
                            Set-ItemProperty -Path $adapter.PSPath -Name 'TcpAckFrequency' -Value 1 -Type DWord -ErrorAction SilentlyContinue
                            Set-ItemProperty -Path $adapter.PSPath -Name 'TCPNoDelay'       -Value 1 -Type DWord -ErrorAction SilentlyContinue
                        }
                        Write-Output 'OK_LATENCY'
                    ";
                    var psResult = await SystemService.RunPowerShellAsync(script);
                    return psResult.Output.Contains("OK_LATENCY")
                        ? Strings.Result_LatencyOptimized
                        : psResult.DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_directx",
                Name        = Strings.Optim_gaming_directx_Name,
                Description = Strings.Optim_gaming_directx_Desc,
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    await Task.Yield();
                    string r1 = SystemService.SetRegistryValue(
                        @"SOFTWARE\Microsoft\DirectX\UserGpuPreferences",
                        "DirectXUserGlobalSettings", "VRROptimizeEnable=0;", RegistryValueKind.String);
                    // Hardware-accelerated GPU scheduling (HAGS)
                    string r2 = SystemService.SetRegistryValue(
                        @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                        "HwSchMode", 2, RegistryValueKind.DWord);
                    return $"{r1}\n{r2}";
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_kill_processes",
                Name        = Strings.Optim_gaming_kill_processes_Name,
                Description = Strings.Optim_gaming_kill_processes_Desc,
                Category    = "Gaming",
                Action = async ct =>
                {
                    // FIX P2-06 : preview + confirmation explicite avant kill (évite fermetures non voulues)
                    string[] candidates = {
                        "OneDrive","Teams","Skype","Discord","Spotify",
                        "EpicGamesLauncher","AdobeUpdateService","CCXProcess"
                    };

                    // Étape 1 — scan sans tuer
                    var running = new List<(string Name, int Count)>();
                    foreach (var name in candidates)
                    {
                        var procs = System.Diagnostics.Process.GetProcessesByName(name);
                        if (procs.Length > 0)
                            running.Add((name, procs.Length));
                        foreach (var p in procs) p.Dispose();
                    }

                    if (running.Count == 0)
                    {
                        await Task.Yield();
                        return Strings.Result_NoTargetProcesses;
                    }

                    // Étape 2 — confirmation avec preview de la liste
                    string preview = string.Join("\n",
                        running.Select(p => $"  • {p.Name} ({p.Count} instance{(p.Count > 1 ? "s" : "")})"));

                    MessageBoxResult confirm = await Application.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show(
                            string.Format(Strings.Result_KillDialogMessage, preview),
                            Strings.Result_KillDialogTitle,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning,
                            MessageBoxResult.No));

                    if (confirm != MessageBoxResult.Yes)
                        return Strings.Result_KillCancelled;

                    // Étape 3 — kill
                    var killed = new HashSet<string>();
                    int skipped = 0;
                    foreach (var (name, _) in running)
                    {
                        var procs = System.Diagnostics.Process.GetProcessesByName(name);
                        foreach (var p in procs)
                        {
                            try
                            {
                                p.Kill();
                                killed.Add(name);
                            }
                            catch (Exception ex)
                            {
                                skipped++;
                                if (skipped <= 3)
                                    LogService.Instance.Warn($"Processus non fermé ({name}) : {ex.Message}", "Gaming");
                            }
                            finally
                            {
                                p.Dispose();
                            }
                        }
                    }

                    if (killed.Count > 0)
                        return string.Format(Strings.Result_ProcessesKilled, string.Join(", ", killed)) +
                               (skipped > 0 ? "\n" + string.Format(Strings.Result_ProcessesNotKilled, skipped) : "");

                    return string.Format(Strings.Result_NoProcessKilled, skipped);
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_fps_unlock",
                Name        = Strings.Optim_gaming_fps_unlock_Name,
                Description = Strings.Optim_gaming_fps_unlock_Desc,
                Category    = "Gaming",
                Action = async ct =>
                {
                    await Task.Yield();
                    string r1 = SystemService.SetRegistryCurrentUser(
                        @"SOFTWARE\Microsoft\DirectX", "UserGpuPreferences",
                        "DirectXUserGlobalSettings=SwapEffectUpgradeEnable=1;", RegistryValueKind.String);
                    return $"{r1}\n{Strings.Result_FpsUnlocked}";
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_gpu_schedule",
                Name        = Strings.Optim_gaming_gpu_schedule_Name,
                Description = Strings.Optim_gaming_gpu_schedule_Desc,
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    await Task.Yield();
                    return SystemService.SetRegistryValue(
                        @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers",
                        "HwSchMode", 2, RegistryValueKind.DWord);
                }
            }
            };

            OptimizationProfileService.ApplyMetadata(items);
            return items;
        }
    }
}
