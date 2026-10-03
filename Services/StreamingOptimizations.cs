using System.Diagnostics;
using System.IO;
using System.Windows;
using Klyr.Models;
using Klyr.Resources;

namespace Klyr.Services
{
    /// <summary>
    /// Module Streaming / Création — optimisations pour streamers, monteurs et créateurs de contenu.
    /// Cible la stabilité de l'encode (NVENC/QSV/AMF), la priorité CPU des logiciels d'encodage
    /// et la libération de ressources pendant l'enregistrement / le live.
    /// </summary>
    public static class StreamingOptimizations
    {
        public static List<OptimizationItem> GetOptimizations()
        {
            var items = new List<OptimizationItem>
            {
                // 1. Mode Streamer — High Perf + désactivation C-States
                new OptimizationItem
                {
                    Id            = "stream_perf_mode",
                    Name          = Strings.Optim_stream_perf_mode_Name,
                    Description   = Strings.Optim_stream_perf_mode_Desc,
                    Category      = "Streaming",
                    RequiresAdmin = true,
                    Action = async ct =>
                    {
                        string script = @"
                            # Plan d'alimentation Hautes performances
                            powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c | Out-Null

                            # Rendre l'option 'Processor idle disable' visible dans powercfg
                            $intelKey = 'HKLM:\SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\5d76a2ca-e8c0-402f-a133-2158492d58ad'
                            if (Test-Path $intelKey) {
                                Set-ItemProperty -Path $intelKey -Name 'Attributes' -Value 2 -Type DWord -ErrorAction SilentlyContinue
                            }

                            # Désactivation C-States sur secteur ET batterie
                            powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR IDLEDISABLE 1 | Out-Null
                            powercfg /setdcvalueindex SCHEME_CURRENT SUB_PROCESSOR IDLEDISABLE 1 | Out-Null
                            powercfg /setactive SCHEME_CURRENT | Out-Null

                            Write-Output 'OK_STREAMER'
                        ";
                        var perfResult = await SystemService.RunPowerShellAsync(script, asAdmin: true);
                        return perfResult.Output.Contains("OK_STREAMER")
                            ? Strings.Result_StreamerModeOn
                            : perfResult.DisplayMessage;
                    }
                },

                // 2. Prio CPU encoder auto — détection logiciels
                new OptimizationItem
                {
                    Id            = "stream_cpu_prio",
                    Name          = Strings.Optim_stream_cpu_prio_Name,
                    Description   = Strings.Optim_stream_cpu_prio_Desc,
                    Category      = "Streaming",
                    RequiresAdmin = true,
                    Action = async ct =>
                    {
                        await Task.Yield();
                        string[] targets =
                        {
                            "obs64", "obs32",
                            "Streamlabs OBS", "Streamlabs Desktop",
                            "Resolve",            // Davinci Resolve
                            "Adobe Premiere Pro",
                            "Adobe Media Encoder",
                            "Vegas Pro",
                            "AfterFX"             // After Effects
                        };

                        int boosted = 0;
                        var detected = new List<string>();

                        foreach (var name in targets)
                        {
                            Process[] procs;
                            try { procs = Process.GetProcessesByName(name); }
                            catch { continue; }

                            foreach (var p in procs)
                            {
                                try
                                {
                                    p.PriorityClass = ProcessPriorityClass.AboveNormal;
                                    boosted++;
                                    if (!detected.Contains(name)) detected.Add(name);
                                }
                                catch
                                {
                                    // Process protégé ou disparu — on ignore
                                }
                            }
                        }

                        if (boosted == 0)
                            return Strings.Result_NoEncoderDetected;

                        return string.Format(Strings.Result_EncoderBoosted, boosted, string.Join(", ", detected));
                    }
                },

                // 3. Game Mode OFF (Avancé)
                new OptimizationItem
                {
                    Id            = "stream_game_mode_off",
                    Name          = Strings.Optim_stream_game_mode_off_Name,
                    Description   = Strings.Optim_stream_game_mode_off_Desc,
                    Category      = "Streaming",
                    RequiresAdmin = true,
                    Action = async ct =>
                    {
                        string script = @"
                            $gameBarKey = 'HKCU:\SOFTWARE\Microsoft\GameBar'
                            $gameConfigKey = 'HKCU:\System\GameConfigStore'

                            if (-not (Test-Path $gameBarKey)) { New-Item -Path $gameBarKey -Force | Out-Null }
                            if (-not (Test-Path $gameConfigKey)) { New-Item -Path $gameConfigKey -Force | Out-Null }

                            Set-ItemProperty -Path $gameBarKey    -Name 'AutoGameModeEnabled' -Value 0 -Type DWord -ErrorAction SilentlyContinue
                            Set-ItemProperty -Path $gameBarKey    -Name 'AllowAutoGameMode'   -Value 0 -Type DWord -ErrorAction SilentlyContinue
                            Set-ItemProperty -Path $gameConfigKey -Name 'GameDVR_Enabled'     -Value 0 -Type DWord -ErrorAction SilentlyContinue
                            Set-ItemProperty -Path $gameConfigKey -Name 'GameDVR_FSEBehaviorMode' -Value 2 -Type DWord -ErrorAction SilentlyContinue

                            Write-Output 'OK_GMOFF'
                        ";
                        var gmResult = await SystemService.RunPowerShellAsync(script, asAdmin: true);
                        return gmResult.Output.Contains("OK_GMOFF")
                            ? Strings.Result_GameModeOff
                            : gmResult.DisplayMessage;
                    }
                },

                // 4. HAGS OFF (Avancé)
                new OptimizationItem
                {
                    Id             = "stream_hags_off",
                    Name           = Strings.Optim_stream_hags_off_Name,
                    Description    = Strings.Optim_stream_hags_off_Desc,
                    Category       = "Streaming",
                    RequiresAdmin  = true,
                    RequiresReboot = true,
                    Action = async ct =>
                    {
                        string script = @"
                            $key = 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers'
                            Set-ItemProperty -Path $key -Name 'HwSchMode' -Value 1 -Type DWord -ErrorAction SilentlyContinue
                            Write-Output 'OK_HAGS'
                        ";
                        var hagsResult = await SystemService.RunPowerShellAsync(script, asAdmin: true);
                        return hagsResult.Output.Contains("OK_HAGS")
                            ? Strings.Result_HagsOff
                            : hagsResult.DisplayMessage;
                    }
                },

                // 5. Killer processus Creator
                new OptimizationItem
                {
                    Id          = "stream_killer",
                    Name        = Strings.Optim_stream_killer_Name,
                    Description = Strings.Optim_stream_killer_Desc,
                    Category    = "Streaming",
                    Action = async ct =>
                    {
                        string[] targets =
                        {
                            "AdobeUpdateService", "Adobe Update Manager", "Adobe Desktop Service",
                            "AdobeNotificationClient", "CCXProcess", "CCLibrary",
                            "MicrosoftEdgeUpdate", "msedge",
                            "Spotify", "SpotifyWebHelper",
                            "Discord",
                            "GitHubDesktop",
                            "ms-teams", "Teams", "msteams",
                            "OneDrive"
                        };

                        // Étape 1 — scan sans tuer
                        var running = new List<(string Name, int Count)>();
                        foreach (var name in targets)
                        {
                            Process[] procs;
                            try { procs = Process.GetProcessesByName(name); }
                            catch { continue; }

                            if (procs.Length > 0)
                                running.Add((name, procs.Length));
                            foreach (var p in procs) p.Dispose();
                        }

                        if (running.Count == 0)
                        {
                            await Task.Yield();
                            return Strings.Result_NoParasites;
                        }

                        // Étape 2 — confirmation avec preview de la liste (comme le module Gaming)
                        string preview = string.Join("\n",
                            running.Select(p => $"  • {p.Name} ({p.Count})"));

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
                        int killed = 0;
                        var seen = new List<string>();

                        foreach (var (name, _) in running)
                        {
                            Process[] procs;
                            try { procs = Process.GetProcessesByName(name); }
                            catch { continue; }

                            foreach (var p in procs)
                            {
                                try
                                {
                                    p.Kill(entireProcessTree: true);
                                    killed++;
                                    if (!seen.Contains(name)) seen.Add(name);
                                }
                                catch
                                {
                                    // Permission denied ou déjà fermé
                                }
                                finally
                                {
                                    p.Dispose();
                                }
                            }
                        }

                        if (killed == 0)
                            return Strings.Result_NoParasites;

                        return string.Format(Strings.Result_ParasitesKilled, killed, string.Join(", ", seen));
                    }
                },

                // 6. Cleanup cache OBS / browser sources
                new OptimizationItem
                {
                    Id          = "stream_clean_cache",
                    Name        = Strings.Optim_stream_clean_cache_Name,
                    Description = Strings.Optim_stream_clean_cache_Desc,
                    Category    = "Streaming",
                    Action = async ct =>
                    {
                        await Task.Yield();

                        string appData      = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

                        var cacheDirs = new[]
                        {
                            Path.Combine(appData,      "obs-studio",   "plugin_config", "obs-browser"),
                            Path.Combine(appData,      "slobs-client", "Cache"),
                            Path.Combine(localAppData, "Streamlabs",   "Streamlabs Desktop", "Cache"),
                            Path.Combine(localAppData, "Streamlabs",   "Streamlabs Desktop", "Code Cache"),
                        };

                        long freedBytes = 0;
                        int cleanedDirs = 0;
                        var cleanedNames = new List<string>();

                        foreach (var dir in cacheDirs)
                        {
                            if (!Directory.Exists(dir)) continue;
                            try
                            {
                                long before = DirectorySize(dir);
                                Directory.Delete(dir, recursive: true);
                                Directory.CreateDirectory(dir);
                                freedBytes += before;
                                cleanedDirs++;
                                cleanedNames.Add(Path.GetFileName(dir));
                            }
                            catch
                            {
                                // Cache verrouillé (OBS lancé) ou permission refusée
                            }
                        }

                        if (cleanedDirs == 0)
                            return Strings.Result_NoObsCache;

                        long freedMb = freedBytes / (1024L * 1024L);
                        return string.Format(Strings.Result_ObsCacheCleared, cleanedDirs, freedMb, string.Join(", ", cleanedNames));
                    }
                },
            };

            OptimizationProfileService.ApplyMetadata(items);
            return items;
        }

        // ───────────────────────────── HELPERS ──────────────────────────────────
        private static long DirectorySize(string path)
        {
            try
            {
                return new DirectoryInfo(path)
                    .EnumerateFiles("*", SearchOption.AllDirectories)
                    .Sum(f => f.Length);
            }
            catch
            {
                return 0;
            }
        }
    }
}
