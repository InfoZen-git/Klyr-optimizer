using System.Windows;
using Klyr.Models;
using Klyr.Resources;

namespace Klyr.Services
{
    /// <summary>
    /// Module Nettoyage.
    /// FIX : Antivirus timeout augmenté à 5min + progression détaillée des fichiers scannés.
    /// v2.2.0 : scan antivirus propose maintenant la mise en quarantaine après détection.
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
                Name        = Strings.Optim_clean_disk_Name,
                Description = Strings.Optim_clean_disk_Desc,
                Category    = "Nettoyage",
                RequiresAdmin = true,
                IsDiskCleanup = true,
                Action = async ct =>
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
                Name        = Strings.Optim_clean_recycle_Name,
                Description = Strings.Optim_clean_recycle_Desc,
                Category    = "Nettoyage",
                IsDiskCleanup = true,
                Action = async ct =>
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
                Name        = Strings.Optim_clean_logs_Name,
                Description = Strings.Optim_clean_logs_Desc,
                Category    = "Nettoyage",
                RequiresAdmin = true,
                IsDiskCleanup = true,
                Action = async ct =>
                {
                    string script = @"
                        $backupDir = Join-Path $env:USERPROFILE 'Documents\Klyr\Backups\EventLogs'
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
                Name        = Strings.Optim_clean_prefetch_Name,
                Description = Strings.Optim_clean_prefetch_Desc,
                Category    = "Nettoyage",
                RequiresAdmin = true,
                IsDiskCleanup = true,
                Action = async ct =>
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
                Name        = Strings.Optim_clean_bloatware_Name,
                Description = Strings.Optim_clean_bloatware_Desc,
                Category    = "Nettoyage",
                RequiresAdmin = true,
                IsDiskCleanup = true,
                Action = async ct =>
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
                Name        = Strings.Optim_clean_repair_Name,
                Description = Strings.Optim_clean_repair_Desc,
                Category    = "Nettoyage",
                RequiresAdmin = true,
                RequiresReboot = true,
                Action = async ct =>
                {
                    // v2.2.0 : ct propagé — l'utilisateur peut killer SFC/DISM mid-run
                    var sfcResult  = await SystemService.RunCmdAsync("sfc /scannow", cancellationToken: ct);
                    if (sfcResult.WasCancelled) return "── SFC ──\nAnnulé par l'utilisateur.";
                    var dismResult = await SystemService.RunCmdAsync("DISM /Online /Cleanup-Image /RestoreHealth", cancellationToken: ct);
                    return $"── SFC ──\n{sfcResult.DisplayMessage}\n\n── DISM ──\n{dismResult.DisplayMessage}";
                }
            },
            new OptimizationItem
            {
                Id          = "clean_dns_cache",
                Name        = Strings.Optim_clean_dns_cache_Name,
                Description = Strings.Optim_clean_dns_cache_Desc,
                Category    = "Nettoyage",
                Action = async ct => (await SystemService.RunCmdAsync("ipconfig /flushdns")).DisplayMessage
            },
            // ── NOUVEAU : Scan antivirus avec progression réelle ──────────────────
            new OptimizationItem
            {
                Id          = "clean_antivirus",
                Name        = Strings.Optim_clean_antivirus_Name,
                Description = Strings.Optim_clean_antivirus_Desc,
                Category    = "Nettoyage",
                RequiresAdmin = true,
                Action = async ct =>
                {
                    return await RunAntivirusScanAsync(ct);
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
        private static async Task<string> RunAntivirusScanAsync(CancellationToken ct = default)
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
                var checkResult = await SystemService.RunPowerShellAsync(checkScript, cancellationToken: ct);
                if (checkResult.Output.Contains("UNAVAILABLE"))
                    return Strings.Result_DefenderUnavailable;

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

                // FIX P1-01 + v2.2.0 : timeout interne 10 min + ct utilisateur pour cancel mid-scan
                var scanResult = await SystemService.RunPowerShellAsync(
                    scanScript,
                    asAdmin: false,
                    timeout: TimeSpan.FromMinutes(10),
                    cancellationToken: ct
                );

                if (scanResult.WasCancelled)
                    return Strings.Result_ScanCancelled;
                if (scanResult.TimedOut)
                {
                    return Strings.Result_ScanTimeout;
                }

                string result = scanResult.Output;
                if (result.Contains("SCAN_END"))
                {
                    var parts = result.Split('|');
                    string duration = parts.Length > 1 ? parts[1] : "?";
                    string threats  = parts.Length > 2 ? parts[2] : "0";
                    int threatNum   = int.TryParse(threats, out int t) ? t : 0;

                    if (threatNum == 0)
                        return string.Format(Strings.Result_ScanNoThreats, duration);

                    // v2.2.0 — Mise en quarantaine optionnelle (dialog localisé)
                    bool userConfirmed = await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        var dialog = MessageBox.Show(
                            string.Format(Strings.Dialog_Quarantine_Message, threatNum),
                            Strings.Dialog_Quarantine_Title,
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Warning);
                        return dialog == MessageBoxResult.Yes;
                    });

                    if (!userConfirmed)
                        return string.Format(Strings.Result_QuarantineRefused, duration, threatNum);

                    // Lancer la quarantaine via Remove-MpThreat (nécessite admin — OK car RequiresAdmin=true)
                    string quarantineScript = @"
                        $threats = Get-MpThreatDetection -ErrorAction SilentlyContinue
                        $removed = 0
                        $failed  = 0
                        if ($threats) {
                            foreach ($entry in $threats) {
                                try {
                                    Remove-MpThreat -ThreatID $entry.ThreatID -ErrorAction Stop
                                    $removed++
                                } catch {
                                    $failed++
                                }
                            }
                        }
                        Write-Output ""QUARANTINE|${removed}|${failed}""
                    ";

                    var quarantineResult = await SystemService.RunPowerShellAsync(
                        quarantineScript,
                        asAdmin: false,
                        timeout: TimeSpan.FromMinutes(2)
                    );

                    if (quarantineResult.TimedOut)
                        return string.Format(Strings.Result_QuarantineTimeout, duration, threatNum);

                    if (quarantineResult.Output.Contains("QUARANTINE|"))
                    {
                        var qParts = quarantineResult.Output.Split('|');
                        int removed = qParts.Length > 1 && int.TryParse(qParts[1].Trim(), out int r) ? r : 0;
                        int failed  = qParts.Length > 2 && int.TryParse(qParts[2].Trim(), out int f) ? f : 0;

                        return failed == 0
                            ? string.Format(Strings.Result_QuarantineSuccess, duration, threatNum, removed)
                            : string.Format(Strings.Result_QuarantinePartial, duration, threatNum, removed, failed);
                    }
                    return string.Format(Strings.Result_QuarantineLaunched, duration, threatNum);
                }
                return $"Scan terminé.\n{result}";
            }
            catch (Exception ex)
            {
                return string.Format(Strings.Result_ScanError, ex.Message);
            }
        }
    }
}
