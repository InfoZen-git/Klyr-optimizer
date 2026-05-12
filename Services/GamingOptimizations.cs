using System.Windows;
using Klyr.Models;
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
                Name        = "Mode Haute Performance",
                Description = "Active le plan d'alimentation Haute Performance Windows pour maximiser les ressources CPU/GPU.",
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async () =>
                {
                    var result = await SystemService.RunCmdAsync("powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                    return result.Success
                        ? "Plan Haute Performance activé."
                        : result.DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_gamedvr",
                Name        = "Désactiver Xbox Game DVR",
                Description = "Supprime l'enregistrement vidéo en arrière-plan Xbox qui consomme CPU, RAM et I/O disque.",
                Category    = "Gaming",
                Action = async () =>
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
                Name        = "Désactiver Fullscreen Optimization",
                Description = "Évite que Windows intercepte le mode plein écran des jeux, réduisant les micro-stutters.",
                Category    = "Gaming",
                Action = async () =>
                {
                    await Task.Yield();
                    return SystemService.SetRegistryCurrentUser(
                        @"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2, RegistryValueKind.DWord);
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_priority",
                Name        = "Priorité CPU pour les Jeux",
                Description = "Configure Windows pour allouer prioritairement les ressources CPU aux applications en premier plan.",
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async () =>
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
                Name        = "Réduction Latence Réseau",
                Description = "Ajuste les paramètres TCP/IP pour réduire le ping en jeu (désactive Nagle, optimise ACK).",
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string script = @"
                        # Désactiver l'algorithme de Nagle
                        $adapters = Get-ChildItem 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces'
                        foreach ($adapter in $adapters) {
                            Set-ItemProperty -Path $adapter.PSPath -Name 'TcpAckFrequency' -Value 1 -Type DWord -ErrorAction SilentlyContinue
                            Set-ItemProperty -Path $adapter.PSPath -Name 'TCPNoDelay'       -Value 1 -Type DWord -ErrorAction SilentlyContinue
                        }
                        Write-Output 'Latence réseau optimisée (Nagle désactivé, TCPNoDelay activé)'
                    ";
                    return (await SystemService.RunPowerShellAsync(script)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_directx",
                Name        = "Tweaks DirectX",
                Description = "Optimise les paramètres DirectX : désactive DXGI flip model debug et ajuste le scheduler GPU.",
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async () =>
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
                Name        = "Fermer Processus Inutiles",
                Description = "Arrête les processus non essentiels pour libérer RAM et CPU avant de lancer un jeu.",
                Category    = "Gaming",
                Action = async () =>
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
                        return "Aucun processus ciblé en cours. Rien à fermer.";
                    }

                    // Étape 2 — confirmation avec preview de la liste
                    string preview = string.Join("\n",
                        running.Select(p => $"  • {p.Name} ({p.Count} instance{(p.Count > 1 ? "s" : "")})"));

                    MessageBoxResult confirm = await Application.Current.Dispatcher.InvokeAsync(() =>
                        MessageBox.Show(
                            $"Les processus suivants vont être fermés :\n\n{preview}\n\n" +
                            "Sauvegardez votre travail dans ces applications avant de continuer.\n\n" +
                            "Continuer ?",
                            "Fermer Processus Inutiles – Klyr",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning,
                            MessageBoxResult.No));

                    if (confirm != MessageBoxResult.Yes)
                        return "Action annulée par l'utilisateur.";

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
                        return $"Processus fermés : {string.Join(", ", killed)}" +
                               (skipped > 0 ? $"\n{skipped} non fermés (droits insuffisants)." : "");

                    return $"Aucun processus n'a pu être fermé ({skipped} échec{(skipped > 1 ? "s" : "")}).";
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_fps_unlock",
                Name        = "Déblocage des FPS",
                Description = "Désactive la limite de fréquence d'images artificielle et le V-Sync forcé de Windows.",
                Category    = "Gaming",
                Action = async () =>
                {
                    await Task.Yield();
                    string r1 = SystemService.SetRegistryCurrentUser(
                        @"SOFTWARE\Microsoft\DirectX", "UserGpuPreferences",
                        "DirectXUserGlobalSettings=SwapEffectUpgradeEnable=1;", RegistryValueKind.String);
                    return $"{r1}\nLimites FPS supprimées.";
                }
            },
            new OptimizationItem
            {
                Id          = "gaming_gpu_schedule",
                Name        = "Optimisation GPU",
                Description = "Active le Hardware-Accelerated GPU Scheduling (HAGS) pour réduire la latence graphique.",
                Category    = "Gaming",
                RequiresAdmin = true,
                Action = async () =>
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
