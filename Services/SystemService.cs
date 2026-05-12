using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Klyr.Models;
using Microsoft.Win32;

namespace Klyr.Services
{
    /// <summary>
    /// Résultat structuré d'une exécution de commande système.
    /// </summary>
    public class CommandResult
    {
        public bool Success { get; set; }
        public string Output { get; set; } = "";
        public string Error { get; set; } = "";
        public int ExitCode { get; set; }
        public bool WasElevated { get; set; }
        public bool TimedOut { get; set; }
        
        /// <summary>Message formaté pour affichage terminal.</summary>
        public string DisplayMessage
        {
            get
            {
                if (TimedOut) return "Timeout : la commande a dépassé le délai imparti.";

                bool hasOutput = !string.IsNullOrWhiteSpace(Output);
                bool hasError  = !string.IsNullOrWhiteSpace(Error);

                if (!hasOutput && !hasError)
                    return Success
                        ? $"Commande exécutée (code {ExitCode}, aucune sortie)."
                        : $"Échec (code {ExitCode}, aucune sortie).";

                if (hasError && hasOutput) return $"{Output.Trim()}\n{Error.Trim()}";
                if (hasError)              return $"{Error.Trim()}";
                return Output.Trim();
            }
        }
        
        public static implicit operator string(CommandResult r) => r.DisplayMessage;
    }

    /// <summary>
    /// Service central d'exécution des commandes système, PowerShell et registre.
    /// FIX P0-01: Retourne maintenant CommandResult structuré au lieu de string vide pour admin.
    /// FIX P1-03: Timeout générique configurable.
    /// </summary>
    public static class SystemService
    {
        /// <summary>Timeout par défaut pour les commandes (5 minutes).</summary>
        public static TimeSpan DefaultTimeout { get; set; } = TimeSpan.FromMinutes(5);

        // ─────────────────────────────── EXÉCUTION ──────────────────────────────

        /// <summary>Exécute une commande CMD et retourne un résultat structuré.</summary>
        public static async Task<CommandResult> RunCmdAsync(string command, bool asAdmin = false, TimeSpan? timeout = null)
        {
            timeout ??= DefaultTimeout;
            string? outFile = null;
            string? errFile = null;
            string? batFile = null;
            try
            {
                ProcessStartInfo psi;

                if (asAdmin)
                {
                    // FIX P0-04: UseShellExecute=true (requis pour runas) empêche la redirection.
                    // On écrit un .bat wrapper qui redirige stdout/stderr vers des fichiers temporaires.
                    outFile = Path.Combine(Path.GetTempPath(), $"klyr_cmd_out_{Guid.NewGuid():N}.txt");
                    errFile = Path.Combine(Path.GetTempPath(), $"klyr_cmd_err_{Guid.NewGuid():N}.txt");
                    batFile = Path.Combine(Path.GetTempPath(), $"klyr_cmd_{Guid.NewGuid():N}.bat");
                    string batContent = $"@echo off\r\nchcp 65001 > nul 2>&1\r\n{command} > \"{outFile}\" 2> \"{errFile}\"\r\nexit /B %ERRORLEVEL%\r\n";
                    await File.WriteAllTextAsync(batFile, batContent, new UTF8Encoding(false));

                    psi = new ProcessStartInfo
                    {
                        FileName        = batFile,
                        UseShellExecute = true,
                        Verb            = "runas",
                        CreateNoWindow  = true,
                        WindowStyle     = ProcessWindowStyle.Hidden
                    };
                }
                else
                {
                    psi = new ProcessStartInfo
                    {
                        FileName               = "cmd.exe",
                        Arguments              = $"/c {command}",
                        UseShellExecute        = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        CreateNoWindow         = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding  = Encoding.UTF8
                    };
                }

                using var proc = Process.Start(psi);
                if (proc == null)
                    return new CommandResult { Success = false, Error = "Impossible de démarrer le processus." };

                string output = "", error = "";
                using var cts = new CancellationTokenSource(timeout.Value);

                if (!asAdmin)
                {
                    var outputTask = proc.StandardOutput.ReadToEndAsync();
                    var errorTask  = proc.StandardError.ReadToEndAsync();
                    try
                    {
                        await proc.WaitForExitAsync(cts.Token);
                        output = await outputTask;
                        error  = await errorTask;
                    }
                    catch (OperationCanceledException)
                    {
                        try { proc.Kill(entireProcessTree: true); }
                        catch (Exception killEx) { LogService.Instance.Warn($"Impossible de terminer le processus CMD après timeout : {killEx.Message}", "SystemService"); }
                        return new CommandResult { Success = false, TimedOut = true, WasElevated = false };
                    }
                }
                else
                {
                    try
                    {
                        await proc.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { proc.Kill(entireProcessTree: true); }
                        catch (Exception killEx) { LogService.Instance.Warn($"Impossible de terminer la commande admin après timeout : {killEx.Message}", "SystemService"); }
                        return new CommandResult { Success = false, TimedOut = true, WasElevated = true };
                    }

                    // FIX P0-04: relire la sortie depuis les fichiers temporaires
                    try
                    {
                        if (File.Exists(outFile)) output = await File.ReadAllTextAsync(outFile!, Encoding.UTF8);
                        if (File.Exists(errFile)) error  = await File.ReadAllTextAsync(errFile!, Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Warn($"Sortie de la commande admin illisible : {ex.Message}", "SystemService");
                    }
                }

                return new CommandResult
                {
                    Success = proc.ExitCode == 0,
                    Output = output,
                    Error = error,
                    ExitCode = proc.ExitCode,
                    WasElevated = asAdmin
                };
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return new CommandResult { Success = false, Error = "Élévation refusée par l'utilisateur (UAC annulé).", WasElevated = true };
            }
            catch (Exception ex)
            {
                return new CommandResult { Success = false, Error = $"Erreur : {ex.Message}" };
            }
            finally
            {
                try { if (outFile != null && File.Exists(outFile)) File.Delete(outFile); } catch { }
                try { if (errFile != null && File.Exists(errFile)) File.Delete(errFile); } catch { }
                try { if (batFile != null && File.Exists(batFile)) File.Delete(batFile); } catch { }
            }
        }

        /// <summary>Exécute un script PowerShell et retourne un résultat structuré.</summary>
        public static async Task<CommandResult> RunPowerShellAsync(string script, bool asAdmin = false, TimeSpan? timeout = null)
        {
            timeout ??= DefaultTimeout;
            string? outFile = null;
            string? errFile = null;
            try
            {
                string finalScript = script;

                if (asAdmin)
                {
                    // FIX P0-04: UseShellExecute=true (requis pour runas) empêche la redirection
                    // stdout/stderr. On encapsule donc le script utilisateur et on redirige ses
                    // sorties vers des fichiers temporaires, lus après exit du process élevé.
                    outFile = Path.Combine(Path.GetTempPath(), $"klyr_ps_out_{Guid.NewGuid():N}.txt");
                    errFile = Path.Combine(Path.GetTempPath(), $"klyr_ps_err_{Guid.NewGuid():N}.txt");
                    string innerEncoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                    string safeOut = outFile.Replace("'", "''");
                    string safeErr = errFile.Replace("'", "''");
                    finalScript = $@"
$ErrorActionPreference = 'Continue'
$__iz_out = '{safeOut}'
$__iz_err = '{safeErr}'
try {{
    $__iz_bytes = [System.Convert]::FromBase64String('{innerEncoded}')
    $__iz_src   = [System.Text.Encoding]::Unicode.GetString($__iz_bytes)
    $__iz_sb    = [ScriptBlock]::Create($__iz_src)
    & $__iz_sb 3>&1 6>&1 1> $__iz_out 2> $__iz_err
    if ($null -eq $LASTEXITCODE) {{ exit 0 }} else {{ exit $LASTEXITCODE }}
}} catch {{
    $_ | Out-File -FilePath $__iz_err -Encoding UTF8 -Append
    exit 1
}}";
                }

                var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(finalScript));
                var psi = new ProcessStartInfo
                {
                    FileName               = "powershell.exe",
                    Arguments              = $"-NonInteractive -NoProfile -EncodedCommand {encoded}",
                    UseShellExecute        = asAdmin,
                    Verb                   = asAdmin ? "runas" : "",
                    RedirectStandardOutput = !asAdmin,
                    RedirectStandardError  = !asAdmin,
                    CreateNoWindow         = true,
                    WindowStyle            = ProcessWindowStyle.Hidden,
                    StandardOutputEncoding = !asAdmin ? Encoding.UTF8 : null,
                    StandardErrorEncoding  = !asAdmin ? Encoding.UTF8 : null
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                    return new CommandResult { Success = false, Error = "Impossible de démarrer PowerShell." };

                string output = "", error = "";
                using var cts = new CancellationTokenSource(timeout.Value);

                if (!asAdmin)
                {
                    var outputTask = proc.StandardOutput.ReadToEndAsync();
                    var errorTask  = proc.StandardError.ReadToEndAsync();
                    try
                    {
                        await proc.WaitForExitAsync(cts.Token);
                        output = await outputTask;
                        error  = await errorTask;
                    }
                    catch (OperationCanceledException)
                    {
                        try { proc.Kill(entireProcessTree: true); }
                        catch (Exception killEx) { LogService.Instance.Warn($"Impossible de terminer PowerShell après timeout : {killEx.Message}", "SystemService"); }
                        return new CommandResult { Success = false, TimedOut = true, WasElevated = false };
                    }
                }
                else
                {
                    try
                    {
                        await proc.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try { proc.Kill(entireProcessTree: true); }
                        catch (Exception killEx) { LogService.Instance.Warn($"Impossible de terminer PowerShell admin après timeout : {killEx.Message}", "SystemService"); }
                        return new CommandResult { Success = false, TimedOut = true, WasElevated = true };
                    }

                    // FIX P0-04: relire la sortie depuis les fichiers temporaires
                    try
                    {
                        if (File.Exists(outFile)) output = await File.ReadAllTextAsync(outFile!, Encoding.UTF8);
                        if (File.Exists(errFile)) error  = await File.ReadAllTextAsync(errFile!, Encoding.UTF8);
                    }
                    catch (Exception ex)
                    {
                        LogService.Instance.Warn($"Sortie du process admin illisible : {ex.Message}", "SystemService");
                    }
                }

                return new CommandResult
                {
                    Success = proc.ExitCode == 0,
                    Output = output,
                    Error = error,
                    ExitCode = proc.ExitCode,
                    WasElevated = asAdmin
                };
            }
            catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
            {
                return new CommandResult { Success = false, Error = "Élévation refusée par l'utilisateur (UAC annulé).", WasElevated = true };
            }
            catch (Exception ex)
            {
                return new CommandResult { Success = false, Error = $"Erreur PowerShell : {ex.Message}" };
            }
            finally
            {
                try { if (outFile != null && File.Exists(outFile)) File.Delete(outFile); } catch { }
                try { if (errFile != null && File.Exists(errFile)) File.Delete(errFile); } catch { }
            }
        }
        
        // Méthodes de compatibilité pour le code existant qui attend string
        [Obsolete("Utilisez la version avec CommandResult pour un meilleur contrôle.")]
        public static async Task<string> RunCmdStringAsync(string command, bool asAdmin = false)
            => (await RunCmdAsync(command, asAdmin)).DisplayMessage;

        [Obsolete("Utilisez la version avec CommandResult pour un meilleur contrôle.")]
        public static async Task<string> RunPowerShellStringAsync(string script, bool asAdmin = false)
            => (await RunPowerShellAsync(script, asAdmin)).DisplayMessage;

        // ─────────────────────────────── REGISTRE ───────────────────────────────

        /// <summary>Définit une valeur dans le registre Windows.</summary>
        public static string SetRegistryValue(string keyPath, string valueName, object value, RegistryValueKind kind)
        {
            try
            {
                using var key = Registry.LocalMachine.CreateSubKey(keyPath, writable: true);
                if (key == null) return $"Impossible d'ouvrir la clé : {keyPath}";
                key.SetValue(valueName, value, kind);
                return $"Registre : {valueName} = {value}";
            }
            catch (UnauthorizedAccessException)
            {
                return "Droits insuffisants. Relancez Klyr en administrateur.";
            }
            catch (Exception ex)
            {
                return $"Erreur registre : {ex.Message}";
            }
        }

        public static string SetRegistryCurrentUser(string keyPath, string valueName, object value, RegistryValueKind kind)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
                if (key == null) return $"Clé introuvable : {keyPath}";
                key.SetValue(valueName, value, kind);
                return $"Registre HKCU : {valueName} = {value}";
            }
            catch (Exception ex)
            {
                return $"Erreur registre : {ex.Message}";
            }
        }

        public static string DeleteRegistryValue(string keyPath, string valueName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
                key?.DeleteValue(valueName, throwOnMissingValue: false);
                return $"Valeur supprimée : {valueName}";
            }
            catch (Exception ex)
            {
                return $"Erreur registre : {ex.Message}";
            }
        }

        // ─────────────────────────────── INFOS SYSTÈME ──────────────────────────

        // FIX P1-06: PerformanceCounter singleton pour éviter de recréer à chaque appel
        private static PerformanceCounter? _cpuCounter;
        private static readonly object _cpuLock = new();
        private static bool _cpuCounterInitialized;

        /// <summary>Collecte les informations système pour le tableau de bord.</summary>
        public static SystemInfoModel GetSystemInfo()
        {
            var info = new SystemInfoModel();
            try
            {
                // OS
                info.OsName      = GetFriendlyOsName();
                info.MachineName = Environment.MachineName;
                info.Uptime      = FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64));

                // RAM
                GetRamInfo(out float usedGb, out float totalGb);
                info.RamUsedGb  = usedGb;
                info.RamTotalGb = totalGb;
                info.RamPercent = totalGb > 0 ? (usedGb / totalGb) * 100f : 0f;

                // CPU Name
                info.CpuName = GetCpuName();

                // Disque C:
                var drive = new DriveInfo("C");
                info.DiskFreeGb  = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                info.DiskTotalGb = drive.TotalSize          / (1024 * 1024 * 1024);
            }
            catch (Exception ex)
            {
                // FIX P2-04: Logger au lieu de silencieux
                LogService.Instance.Warn($"Erreur récupération infos système : {ex.Message}", "SystemService");
            }
            return info;
        }

        /// <summary>
        /// Retourne l'utilisation CPU actuelle.
        /// FIX P1-06: Utilise un PerformanceCounter singleton.
        /// </summary>
        public static float GetCpuUsage()
        {
            try
            {
                lock (_cpuLock)
                {
                    if (!_cpuCounterInitialized)
                    {
                        try
                        {
                            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                            _cpuCounter.NextValue(); // Premier appel pour initialiser
                        }
                        catch (Exception ex)
                        {
                            LogService.Instance.Warn($"PerformanceCounter indisponible : {ex.Message}", "SystemService");
                            _cpuCounter = null;
                        }
                        _cpuCounterInitialized = true;
                    }
                }

                if (_cpuCounter == null) return 0f;
                
                // Pas de Sleep ici - le timer du VM appelle déjà toutes les 2s
                return _cpuCounter.NextValue();
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Erreur lecture CPU : {ex.Message}", "SystemService");
                return 0f;
            }
        }

        /// <summary>
        /// Stats temps réel : RAM + disque C: + uptime. Léger (pas de WMI / registre).
        /// Utilisé pour le refresh périodique du Dashboard sans surcoût.
        /// </summary>
        public static (float RamUsedGb, float RamTotalGb, float RamPercent,
                       long DiskFreeGb, long DiskTotalGb, string Uptime) GetRealtimeStats()
        {
            GetRamInfo(out float used, out float total);
            float pct = total > 0 ? (used / total) * 100f : 0f;

            long diskFree = 0, diskTotal = 0;
            try
            {
                var drive = new DriveInfo("C");
                diskFree  = drive.AvailableFreeSpace / (1024L * 1024 * 1024);
                diskTotal = drive.TotalSize          / (1024L * 1024 * 1024);
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Lecture disque C: échouée : {ex.Message}", "SystemService");
            }

            string uptime = FormatUptime(TimeSpan.FromMilliseconds(Environment.TickCount64));
            return (used, total, pct, diskFree, diskTotal, uptime);
        }

        /// <summary>Libère les ressources du service (appeler à la fermeture de l'app).</summary>
        public static void Cleanup()
        {
            lock (_cpuLock)
            {
                _cpuCounter?.Dispose();
                _cpuCounter = null;
                _cpuCounterInitialized = false;
            }
        }

        private static string GetCpuName()
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
                return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "CPU inconnu";
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Impossible de lire le nom du CPU : {ex.Message}", "SystemService");
                return "CPU inconnu";
            }
        }

        /// <summary>
        /// Nom convivial de l'OS, ex. "Windows 11 25H2".
        /// Détecte Win10/11 via le numéro de build (>= 22000 → Win 11) et lit DisplayVersion
        /// dans HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion.
        /// Note : ProductName n'est pas fiable sur Win 11 (Microsoft ne l'a jamais mis à jour).
        /// </summary>
        private static string GetFriendlyOsName()
        {
            try
            {
                int build = Environment.OSVersion.Version.Build;
                string majorName = build >= 22000 ? "Windows 11" : "Windows 10";

                string? displayVersion = null;
                try
                {
                    using var key = Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
                    displayVersion = key?.GetValue("DisplayVersion") as string;
                }
                catch (Exception ex)
                {
                    LogService.Instance.Warn($"Lecture DisplayVersion échouée : {ex.Message}", "SystemService");
                }

                return string.IsNullOrWhiteSpace(displayVersion)
                    ? majorName
                    : $"{majorName} {displayVersion}";
            }
            catch
            {
                return Environment.OSVersion.ToString();
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint  dwLength;
            public uint  dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }
        [DllImport("kernel32.dll")]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private static void GetRamInfo(out float usedGb, out float totalGb)
        {
            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref mem))
            {
                totalGb = mem.ullTotalPhys / (1024f * 1024f * 1024f);
                float freeGb = mem.ullAvailPhys / (1024f * 1024f * 1024f);
                usedGb = totalGb - freeGb;
            }
            else { usedGb = 0; totalGb = 0; }
        }

        private static string FormatUptime(TimeSpan ts)
        {
            if (ts.TotalHours < 1) return $"{ts.Minutes}m";
            if (ts.TotalDays  < 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{(int)ts.TotalDays}j {ts.Hours}h";
        }

        // ─────────────────────────────── RESTAURATION ───────────────────────────

        /// <summary>
        /// Crée un point de restauration Windows.
        /// FIX P0-03: Exige admin et valide la création.
        /// </summary>
        public static async Task<CommandResult> CreateRestorePointAsync(string description = "Klyr – avant optimisation")
        {
            // Vérifier d'abord si on est admin
            if (!AdminChecker.IsRunningAsAdmin())
            {
                return new CommandResult
                {
                    Success = false,
                    Error = "Droits administrateur requis pour créer un point de restauration. Relancez Klyr en admin."
                };
            }

            string safeDescription = description.Replace("'", "''");
            string script = $@"
                try {{
                    Enable-ComputerRestore -Drive 'C:\' -ErrorAction Stop | Out-Null

                    $beforePoints = Get-ComputerRestorePoint -ErrorAction SilentlyContinue
                    $beforeCount = @($beforePoints).Count

                    Checkpoint-Computer -Description '{safeDescription}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop
                    Start-Sleep -Seconds 2

                    $afterPoints = Get-ComputerRestorePoint -ErrorAction SilentlyContinue
                    $afterCount = @($afterPoints).Count
                    $exactMatch = $afterPoints |
                        Where-Object {{ $_.Description -eq '{safeDescription}' }} |
                        Sort-Object SequenceNumber -Descending |
                        Select-Object -First 1

                    if ($exactMatch) {{
                        Write-Output ""SUCCESS|$($exactMatch.SequenceNumber)|{safeDescription}""
                    }} elseif ($afterCount -gt $beforeCount) {{
                        Write-Output ""WARN|Point créé mais description non retrouvée exactement (limite Windows).|$beforeCount|$afterCount""
                    }} else {{
                        Write-Output 'ERROR|Aucun nouveau point de restauration détecté après exécution.'
                    }}
                }} catch {{
                    Write-Output ""ERROR|$($_.Exception.Message)""
                }}
            ";
            
            // Important: exécuter sans asAdmin pour capturer la sortie et valider réellement.
            var result = await RunPowerShellAsync(script);

            if (!result.Success && string.IsNullOrWhiteSpace(result.Output))
                return result;

            string marker = "";
            var outputLines = result.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (outputLines.Length > 0)
                marker = outputLines[^1].Trim();

            if (marker.StartsWith("SUCCESS|", StringComparison.Ordinal))
            {
                result.Success = true;
                var parts = marker.Split('|');
                string sequence = parts.Length > 1 ? parts[1] : "?";
                string label = parts.Length > 2 ? parts[2] : description;
                result.Output = $"Point de restauration créé : {label} (ID {sequence}).";
                result.Error = "";
            }
            else if (marker.StartsWith("WARN|", StringComparison.Ordinal))
            {
                result.Success = false;
                var parts = marker.Split('|');
                result.Error = parts.Length > 1
                    ? $"{parts[1]}"
                    : "Point de restauration non confirmé.";
                result.Output = "";
            }
            else if (marker.StartsWith("ERROR|", StringComparison.Ordinal))
            {
                result.Success = false;
                var parts = marker.Split('|');
                result.Error = parts.Length > 1 ? $"{parts[1]}" : "Échec de création du point de restauration.";
                result.Output = "";
            }
            else
            {
                result.Success = false;
                result.Error = string.IsNullOrWhiteSpace(marker)
                    ? "Validation du point de restauration impossible (sortie vide)."
                    : $"Validation du point de restauration impossible : {marker}";
                result.Output = "";
            }
            
            return result;
        }

        // ─────────────────────────────── LOGS ───────────────────────────────────

        /// <summary>Exporte les logs dans un fichier .txt.</summary>
        public static string ExportLogs(string logContent)
        {
            try
            {
                string dir  = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Klyr", "Logs");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"Klyr_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(file, logContent, Encoding.UTF8);
                return file;
            }
            catch (Exception ex)
            {
                return $"Export échoué : {ex.Message}";
            }
        }
    }
}
