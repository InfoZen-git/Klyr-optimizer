using System.Runtime.InteropServices;
using Klyr.Models;
using Microsoft.Win32;

namespace Klyr.Services
{
    /// <summary>
    /// Module Vieux PC – optimisations pour machines anciennes ou peu puissantes.
    /// FIX : Libération RAM utilise maintenant EmptyWorkingSet (API Win32 réelle)
    /// </summary>
    public static class OldPcOptimizations
    {
        // ── API Win32 pour vider les Working Sets (fix bug communauté) ──
        [DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);

        public static List<OptimizationItem> GetOptimizations()
        {
            var items = new List<OptimizationItem>
            {
            new OptimizationItem
            {
                Id          = "oldpc_startup",
                Name        = "Réduire le Démarrage",
                Description = "Désactive les programmes inutiles au démarrage Windows.",
                Category    = "VieuxPC",
                Action = async () =>
                {
                    string script = @"
                        $startupPaths = @(
                            'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run',
                            'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run'
                        )
                        $toDisable = @('OneDrive','Spotify','Discord','Teams','Skype','EpicGamesLauncher')
                        $disabled = @()
                        foreach ($path in $startupPaths) {
                            foreach ($app in $toDisable) {
                                $val = Get-ItemProperty -Path $path -Name $app -ErrorAction SilentlyContinue
                                if ($val) {
                                    Remove-ItemProperty -Path $path -Name $app -ErrorAction SilentlyContinue
                                    $disabled += $app
                                }
                            }
                        }
                        if ($disabled.Count -gt 0) {
                            Write-Output ""Désactivés au démarrage : $($disabled -join ', ')""
                        } else {
                            Write-Output 'Aucun programme ciblé trouvé au démarrage.'
                        }
                    ";
                    return (await SystemService.RunPowerShellAsync(script)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "oldpc_animations",
                Name        = "Désactiver les Animations",
                Description = "Supprime les effets visuels gourmands pour fluidifier l'interface.",
                Category    = "VieuxPC",
                Action = async () =>
                {
                    await Task.Yield();
                    string r = SystemService.SetRegistryCurrentUser(
                        @"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
                        "VisualFXSetting", 2, RegistryValueKind.DWord);
                    string script = @"
                        Set-ItemProperty -Path 'HKCU:\Control Panel\Desktop' -Name 'UserPreferencesMask' `
                            -Value ([byte[]](0x90,0x12,0x03,0x80,0x10,0x00,0x00,0x00)) -ErrorAction SilentlyContinue
                        Write-Output 'Animations Windows désactivées'
                    ";
                    var r2 = await SystemService.RunPowerShellAsync(script);
                    return $"{r}\n{r2.DisplayMessage}";
                }
            },
            new OptimizationItem
            {
                Id          = "oldpc_registry",
                Name        = "Nettoyage du Registre",
                Description = "Supprime les entrées MRU et clés orphelines du registre Windows.",
                Category    = "VieuxPC",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string script = @"
                        $paths = @(
                            'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RecentDocs',
                            'HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\RunMRU'
                        )
                        $cleaned = 0
                        foreach ($p in $paths) {
                            if (Test-Path $p) {
                                Get-Item -Path $p | Select-Object -ExpandProperty Property | ForEach-Object {
                                    Remove-ItemProperty -Path $p -Name $_ -ErrorAction SilentlyContinue
                                    $cleaned++
                                }
                            }
                        }
                        Write-Output ""Registre nettoyé : $cleaned entrées supprimées""
                    ";
                    return (await SystemService.RunPowerShellAsync(script)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "oldpc_ram",
                Name        = "Libération de RAM",
                Description = "Vide les Working Sets de tous les processus via l'API Win32 EmptyWorkingSet (méthode réelle et efficace).",
                Category    = "VieuxPC",
                RequiresAdmin = true,
                Action = async () =>
                {
                    return await Task.Run(() =>
                    {
                        try
                        {
                            // Mesure AVANT
                            GetRamFree(out long freeBefore, out long total);

                            int success = 0, failed = 0, skipped = 0;
                            int currentProcessId = Environment.ProcessId;
                            var protectedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                            {
                                "System", "Idle", "csrss", "wininit", "services", "lsass",
                                "smss", "dwm", "explorer", "winlogon", "fontdrvhost"
                            };
                            var processes = System.Diagnostics.Process.GetProcesses();

                            foreach (var proc in processes)
                            {
                                try
                                {
                                    bool isForegroundApp = proc.MainWindowHandle != IntPtr.Zero;
                                    bool isSessionZero = proc.SessionId == 0;
                                    bool isCurrent = proc.Id == currentProcessId;
                                    bool isProtected = protectedNames.Contains(proc.ProcessName);

                                    if (isForegroundApp || isSessionZero || isCurrent || isProtected)
                                    {
                                        skipped++;
                                        continue;
                                    }

                                    // EmptyWorkingSet force Windows à écrire les pages mémoire
                                    // non utilisées sur le disque (page file) → libère la RAM physique
                                    if (EmptyWorkingSet(proc.Handle))
                                        success++;
                                    else
                                        failed++;
                                }
                                catch (Exception ex)
                                {
                                    failed++;
                                    if (failed <= 3)
                                        LogService.Instance.Warn($"Processus ignoré pendant libération RAM : {ex.Message}", "VieuxPC");
                                }
                                finally { proc.Dispose(); }
                            }

                            // .NET GC en complément
                            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
                            GC.WaitForPendingFinalizers();
                            GC.Collect(2, GCCollectionMode.Forced, blocking: true);

                            // Mesure APRÈS
                            GetRamFree(out long freeAfter, out _);
                            long gainMb = (freeAfter - freeBefore) / (1024 * 1024);

                            return gainMb > 0
                                ? $"RAM libérée : +{gainMb} Mo — {success} processus background optimisés, {skipped} protégés, {failed} ignorés."
                                : $"Working Sets vidés sur {success} processus background. {skipped} processus protégés laissés intacts.";
                        }
                        catch (Exception ex)
                        {
                            return $"Erreur : {ex.Message} — Relancez en administrateur.";
                        }
                    });
                }
            },
            new OptimizationItem
            {
                Id          = "oldpc_temp",
                Name        = "Suppression Fichiers Temporaires",
                Description = "Efface les dossiers %TEMP%, C:\\Windows\\Temp et fichiers inutiles.",
                Category    = "VieuxPC",
                Action = async () =>
                {
                    string script = @"
                        $paths = @($env:TEMP, 'C:\Windows\Temp')
                        $totalSize = 0
                        $totalFiles = 0
                        foreach ($p in $paths) {
                            if (Test-Path $p) {
                                $files = Get-ChildItem -Path $p -Recurse -Force -ErrorAction SilentlyContinue
                                $totalSize += ($files | Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum
                                $totalFiles += $files.Count
                                Remove-Item -Path ""$p\*"" -Recurse -Force -ErrorAction SilentlyContinue
                            }
                        }
                        $sizeMb = [math]::Round($totalSize / 1MB, 1)
                        Write-Output ""$totalFiles fichiers supprimés ($sizeMb Mo libérés)""
                    ";
                    return (await SystemService.RunPowerShellAsync(script)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "oldpc_defrag",
                Name        = "Défragmentation HDD",
                Description = "Lance la défragmentation du disque C: (HDD uniquement – ignoré sur SSD).",
                Category    = "VieuxPC",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string checkScript = @"
                        try {
                            $systemDrive = $env:SystemDrive.TrimEnd('\').TrimEnd(':')
                            $partition = Get-Partition -DriveLetter $systemDrive -ErrorAction Stop
                            $disk = Get-Disk -Number $partition.DiskNumber -ErrorAction Stop
                            if ($disk.MediaType -eq 'SSD' -or $disk.FriendlyName -match 'SSD') {
                                Write-Output ""SSD|$($disk.FriendlyName)""
                            } elseif ($disk.MediaType -eq 'HDD') {
                                Write-Output ""HDD|$($disk.FriendlyName)""
                            } else {
                                Write-Output ""UNKNOWN|$($disk.MediaType)|$($disk.FriendlyName)""
                            }
                        } catch {
                            Write-Output ""ERROR|$($_.Exception.Message)""
                        }
                    ";
                    var diskTypeResult = await SystemService.RunPowerShellAsync(checkScript);
                    string diskInfo = diskTypeResult.Output.Trim();

                    if (diskInfo.StartsWith("SSD|", StringComparison.Ordinal))
                        return "SSD détecté : défragmentation annulée (inutile et déconseillée sur SSD).";

                    if (diskInfo.StartsWith("ERROR|", StringComparison.Ordinal))
                    {
                        int sep = diskInfo.IndexOf('|');
                        string detail = sep >= 0 && sep + 1 < diskInfo.Length
                            ? diskInfo[(sep + 1)..]
                            : "erreur inconnue";
                        return $"Détection du disque système impossible : {detail}";
                    }

                    if (diskInfo.StartsWith("UNKNOWN|", StringComparison.Ordinal))
                        return $"Type de disque non déterminé ({diskInfo}). Défragmentation annulée par sécurité.";

                    return (await SystemService.RunCmdAsync("defrag C: /U /V")).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "oldpc_theme",
                Name        = "Activer Thème Léger",
                Description = "Désactive la transparence et les effets Aero pour économiser le GPU.",
                Category    = "VieuxPC",
                Action = async () =>
                {
                    await Task.Yield();
                    string r1 = SystemService.SetRegistryCurrentUser(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "EnableTransparency", 0, RegistryValueKind.DWord);
                    string r2 = SystemService.SetRegistryCurrentUser(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                        "AppsUseLightTheme", 1, RegistryValueKind.DWord);
                    return $"{r1}\n{r2}\nThème léger activé, transparence désactivée.";
                }
            },
            new OptimizationItem
            {
                Id          = "oldpc_telemetry",
                Name        = "Désactiver la Télémétrie",
                Description = "Réduit les envois de données à Microsoft (DiagTrack, CEIP).",
                Category    = "VieuxPC",
                RequiresAdmin = true,
                Action = async () =>
                {
                    await Task.Yield();
                    string r1 = SystemService.SetRegistryValue(
                        @"SOFTWARE\Policies\Microsoft\Windows\DataCollection",
                        "AllowTelemetry", 0, RegistryValueKind.DWord);
                    string script = @"
                        Stop-Service DiagTrack -Force -ErrorAction SilentlyContinue
                        Set-Service  DiagTrack -StartupType Disabled -ErrorAction SilentlyContinue
                        Stop-Service dmwappushservice -Force -ErrorAction SilentlyContinue
                        Set-Service  dmwappushservice -StartupType Disabled -ErrorAction SilentlyContinue
                        Write-Output 'Services télémétrie désactivés (DiagTrack, dmwappushservice)'
                    ";
                    var r2 = await SystemService.RunPowerShellAsync(script);
                    return $"{r1}\n{r2.DisplayMessage}";
                }
            }
            };

            OptimizationProfileService.ApplyMetadata(items);
            return items;
        }

        private static void GetRamFree(out long freeBytes, out long totalBytes)
        {
            freeBytes = totalBytes = 0;
            try
            {
                var query = new System.Management.ManagementObjectSearcher("SELECT * FROM Win32_OperatingSystem");
                foreach (System.Management.ManagementObject obj in query.Get())
                {
                    totalBytes = Convert.ToInt64(obj["TotalVisibleMemorySize"]) * 1024L;
                    freeBytes  = Convert.ToInt64(obj["FreePhysicalMemory"])     * 1024L;
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Impossible de lire la RAM libre : {ex.Message}", "VieuxPC");
            }
        }
    }
}
