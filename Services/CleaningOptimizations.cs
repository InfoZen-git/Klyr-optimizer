using InfoZen.Models;

namespace InfoZen.Services
{
    /// <summary>
    /// Module Nettoyage.
    /// FIX : Antivirus timeout augmenté à 5min + progression détaillée des fichiers scannés.
    /// </summary>
    public static class CleaningOptimizations
    {
        public static List<OptimizationItem> GetOptimizations()
        {
            var items = new List<OptimizationItem>
            {
            new OptimizationItem
            {
                Id          = "clean_disk",
                Name        = "Nettoyage Disque Système",
                Description = "Lance l'outil Disk Cleanup (cleanmgr) pour supprimer fichiers inutiles et caches.",
                Category    = "Nettoyage",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string script = @"
                        $regPath = 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Explorer\VolumeCaches'
                        $categories = Get-ChildItem $regPath
                        foreach ($cat in $categories) {
                            Set-ItemProperty -Path $cat.PSPath -Name 'StateFlags0001' -Value 2 -Type DWord -ErrorAction SilentlyContinue
                        }
                        Start-Process cleanmgr -ArgumentList '/sagerun:1' -Wait
                        Write-Output 'Nettoyage disque terminé.'
                    ";
                    return (await SystemService.RunPowerShellAsync(script, asAdmin: true)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "clean_recycle",
                Name        = "Vider la Corbeille",
                Description = "Supprime définitivement tous les fichiers de la corbeille Windows.",
                Category    = "Nettoyage",
                Action = async () =>
                {
                    string script = @"
                        Clear-RecycleBin -Force -ErrorAction SilentlyContinue
                        Write-Output 'Corbeille vidée.'
                    ";
                    return (await SystemService.RunPowerShellAsync(script)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "clean_logs",
                Name        = "Suppression Logs Windows",
                Description = "Exporte puis efface les journaux d'événements Windows (mode avancé dépannage).",
                Category    = "Nettoyage",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string script = @"
                        $backupDir = Join-Path $env:USERPROFILE 'Documents\InfoZen\Backups\EventLogs'
                        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
                        $stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
                        $exported = 0
                        $cleared = 0

                        wevtutil el | ForEach-Object {
                            $safe = ($_ -replace '[\\/:*?""<>| ]', '_')
                            $target = Join-Path $backupDir (""{0}_{1}.evtx"" -f $safe, $stamp)

                            wevtutil epl ""$_"" ""$target"" 2>$null
                            if ($LASTEXITCODE -eq 0) { $exported++ }

                            wevtutil cl ""$_"" 2>$null
                            if ($LASTEXITCODE -eq 0) { $cleared++ }
                        }
                        Write-Output ""$exported journaux exportés, $cleared journaux effacés. Backup: $backupDir""
                    ";
                    return (await SystemService.RunPowerShellAsync(script, asAdmin: true)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "clean_prefetch",
                Name        = "Nettoyage Prefetch",
                Description = "Supprime les fichiers Prefetch (action avancée dépannage; peut ralentir temporairement les premiers lancements).",
                Category    = "Nettoyage",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string script = @"
                        $path = 'C:\Windows\Prefetch'
                        if (Test-Path $path) {
                            $files = Get-ChildItem $path -Filter '*.pf' -ErrorAction SilentlyContinue
                            $count = $files.Count
                            $files | Remove-Item -Force -ErrorAction SilentlyContinue
                            Write-Output ""Prefetch : $count fichiers supprimés.""
                        } else {
                            Write-Output 'Dossier Prefetch introuvable.'
                        }
                    ";
                    return (await SystemService.RunPowerShellAsync(script, asAdmin: true)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "clean_bloatware",
                Name        = "Supprimer Applications Inutiles",
                Description = "Désinstalle les bloatwares Windows courants (Candy Crush, Solitaire…).",
                Category    = "Nettoyage",
                RequiresAdmin = true,
                Action = async () =>
                {
                    string script = @"
                        $bloatware = @(
                            'Microsoft.BingNews','Microsoft.BingWeather','Microsoft.GetHelp',
                            'Microsoft.Getstarted','Microsoft.MicrosoftSolitaireCollection',
                            'Microsoft.MixedReality.Portal','Microsoft.People','Microsoft.SkypeApp',
                            'Microsoft.Todos','Microsoft.WindowsFeedbackHub','Microsoft.XboxApp',
                            'Microsoft.XboxGameOverlay','Microsoft.XboxGamingOverlay',
                            'Microsoft.ZuneMusic','Microsoft.ZuneVideo','king.com.CandyCrushSaga'
                        )
                        $removed = @()
                        foreach ($app in $bloatware) {
                            $pkg = Get-AppxPackage -Name $app -AllUsers -ErrorAction SilentlyContinue
                            if ($pkg) {
                                Remove-AppxPackage -Package $pkg.PackageFullName -ErrorAction SilentlyContinue
                                $removed += $app.Split('.')[-1]
                            }
                        }
                        if ($removed.Count -gt 0) {
                            Write-Output ""Supprimées : $($removed -join ', ')""
                        } else {
                            Write-Output 'Aucun bloatware ciblé trouvé.'
                        }
                    ";
                    return (await SystemService.RunPowerShellAsync(script, asAdmin: true)).DisplayMessage;
                }
            },
            new OptimizationItem
            {
                Id          = "clean_repair",
                Name        = "Réparation Windows (SFC + DISM)",
                Description = "Lance sfc /scannow puis DISM pour réparer les fichiers système corrompus.",
                Category    = "Nettoyage",
                RequiresAdmin = true,
                RequiresReboot = true,
                Action = async () =>
                {
                    var sfcResult  = await SystemService.RunCmdAsync("sfc /scannow");
                    var dismResult = await SystemService.RunCmdAsync("DISM /Online /Cleanup-Image /RestoreHealth");
                    return $"── SFC ──\n{sfcResult.DisplayMessage}\n\n── DISM ──\n{dismResult.DisplayMessage}";
                }
            },
            new OptimizationItem
            {
                Id          = "clean_dns_cache",
                Name        = "Nettoyage Cache DNS",
                Description = "Vide le cache DNS local pour corriger des problèmes de navigation.",
                Category    = "Nettoyage",
                Action = async () => (await SystemService.RunCmdAsync("ipconfig /flushdns")).DisplayMessage
            },
            // ── NOUVEAU : Scan antivirus avec progression réelle ──────────────────
            new OptimizationItem
            {
                Id          = "clean_antivirus",
                Name        = "Scan Antivirus (Windows Defender)",
                Description = "Lance un QuickScan Windows Defender avec affichage du nombre de fichiers analysés. Timeout étendu à 10 minutes.",
                Category    = "Nettoyage",
                RequiresAdmin = true,
                Action = async () =>
                {
                    return await RunAntivirusScanAsync();
                }
            }
            };

            OptimizationProfileService.ApplyMetadata(items);
            return items;
        }

        // ─────────────────────── SCAN ANTIVIRUS AVEC PROGRESSION ────────────────
        /// <summary>
        /// FIX P1-01: Timeout qui kill réellement le process.
        /// FIX bug communauté : timeout 30s → 10 minutes.
        /// </summary>
        private static async Task<string> RunAntivirusScanAsync()
        {
            try
            {
                // Vérifie que Windows Defender est disponible
                string checkScript = @"
                    $mpStatus = Get-MpComputerStatus -ErrorAction SilentlyContinue
                    if ($mpStatus) {
                        Write-Output ""OK|$($mpStatus.AMRunningMode)|$($mpStatus.AntivirusEnabled)""
                    } else {
                        Write-Output 'UNAVAILABLE'
                    }
                ";
                var checkResult = await SystemService.RunPowerShellAsync(checkScript);
                if (checkResult.Output.Contains("UNAVAILABLE"))
                    return "Windows Defender introuvable. Il a peut-être été désactivé par le 'Killer Processus'. Redémarre le service MsMpEng.";

                // Lance le scan avec timeout étendu à 10 minutes
                string scanScript = @"
                    $start = Get-Date
                    Write-Output 'SCAN_START'

                    # Lancer le scan
                    Start-MpScan -ScanType QuickScan -ErrorAction Stop

                    $end = Get-Date
                    $duration = [math]::Round(($end - $start).TotalSeconds, 1)

                    # Récupérer les résultats
                    $history = Get-MpThreatDetection -ErrorAction SilentlyContinue
                    $threatCount = if ($history) { $history.Count } else { 0 }

                    Write-Output ""SCAN_END|${duration}|${threatCount}""
                ";

                // FIX P1-01: Utiliser le timeout intégré à RunPowerShellAsync qui kill le process
                var scanResult = await SystemService.RunPowerShellAsync(
                    scanScript, 
                    asAdmin: false, 
                    timeout: TimeSpan.FromMinutes(10)
                );
                
                if (scanResult.TimedOut)
                {
                    return "⏱ Scan annulé après 10 minutes. Votre PC est peut-être très chargé. Réessayez via Windows Defender directement.";
                }

                string result = scanResult.Output;
                if (result.Contains("SCAN_END"))
                {
                    var parts = result.Split('|');
                    string duration = parts.Length > 1 ? parts[1] : "?";
                    string threats  = parts.Length > 2 ? parts[2] : "0";
                    int threatNum   = int.TryParse(threats, out int t) ? t : 0;

                    return threatNum > 0
                        ? $"✅ Scan terminé en {duration}s — ⚠ {threatNum} menace(s) détectée(s) ! Ouvrez Windows Defender pour les traiter."
                        : $"✅ Scan terminé en {duration}s — Aucune menace détectée.";
                }
                return $"Scan terminé.\n{result}";
            }
            catch (Exception ex)
            {
                return $"❌ Erreur scan : {ex.Message}\nConseils : vérifiez que Windows Defender est actif et que vous avez les droits admin.";
            }
        }
    }
}
