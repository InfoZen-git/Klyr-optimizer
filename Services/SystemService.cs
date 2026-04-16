using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using InfoZen.Models;
using Microsoft.Win32;

namespace InfoZen.Services
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
                if (TimedOut) return "⏱ Timeout : la commande a dépassé le délai imparti.";
                if (WasElevated && string.IsNullOrEmpty(Output) && string.IsNullOrEmpty(Error))
                    return Success ? "✅ Commande admin exécutée (sortie non capturée)." : "⚠ Commande admin lancée (vérifiez manuellement).";
                if (!string.IsNullOrWhiteSpace(Error))
                    return $"{Output.Trim()}\n⚠ {Error.Trim()}";
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
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "cmd.exe",
                    Arguments              = $"/c {command}",
                    UseShellExecute        = asAdmin,
                    Verb                   = asAdmin ? "runas" : "",
                    RedirectStandardOutput = !asAdmin,
                    RedirectStandardError  = !asAdmin,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = !asAdmin ? Encoding.UTF8 : null,
                    StandardErrorEncoding  = !asAdmin ? Encoding.UTF8 : null
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                    return new CommandResult { Success = false, Error = "Impossible de démarrer le processus." };

                string output = "", error = "";
                if (!asAdmin)
                {
                    var outputTask = proc.StandardOutput.ReadToEndAsync();
                    var errorTask = proc.StandardError.ReadToEndAsync();
                    
                    using var cts = new CancellationTokenSource(timeout.Value);
                    try
                    {
                        await proc.WaitForExitAsync(cts.Token);
                        output = await outputTask;
                        error = await errorTask;
                    }
                    catch (OperationCanceledException)
                    {
                        try
                        {
                            proc.Kill(entireProcessTree: true);
                        }
                        catch (Exception killEx)
                        {
                            LogService.Instance.Warn($"Impossible de terminer le processus CMD après timeout : {killEx.Message}", "SystemService");
                        }
                        return new CommandResult { Success = false, TimedOut = true, WasElevated = asAdmin };
                    }
                }
                else
                {
                    // Pour les commandes admin, on attend mais sans timeout strict car UAC peut bloquer
                    using var cts = new CancellationTokenSource(timeout.Value);
                    try
                    {
                        await proc.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try
                        {
                            proc.Kill(entireProcessTree: true);
                        }
                        catch (Exception killEx)
                        {
                            LogService.Instance.Warn($"Impossible de terminer la commande admin après timeout : {killEx.Message}", "SystemService");
                        }
                        return new CommandResult { Success = false, TimedOut = true, WasElevated = true };
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
                // L'utilisateur a annulé l'UAC
                return new CommandResult { Success = false, Error = "❌ Élévation refusée par l'utilisateur (UAC annulé).", WasElevated = true };
            }
            catch (Exception ex)
            {
                return new CommandResult { Success = false, Error = $"❌ Erreur : {ex.Message}" };
            }
        }

        /// <summary>Exécute un script PowerShell et retourne un résultat structuré.</summary>
        public static async Task<CommandResult> RunPowerShellAsync(string script, bool asAdmin = false, TimeSpan? timeout = null)
        {
            timeout ??= DefaultTimeout;
            try
            {
                var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
                var psi = new ProcessStartInfo
                {
                    FileName               = "powershell.exe",
                    Arguments              = $"-NonInteractive -NoProfile -EncodedCommand {encoded}",
                    UseShellExecute        = asAdmin,
                    Verb                   = asAdmin ? "runas" : "",
                    RedirectStandardOutput = !asAdmin,
                    RedirectStandardError  = !asAdmin,
                    CreateNoWindow         = true,
                    StandardOutputEncoding = !asAdmin ? Encoding.UTF8 : null,
                    StandardErrorEncoding  = !asAdmin ? Encoding.UTF8 : null
                };

                using var proc = Process.Start(psi);
                if (proc == null)
                    return new CommandResult { Success = false, Error = "Impossible de démarrer PowerShell." };

                string output = "", error = "";
                if (!asAdmin)
                {
                    var outputTask = proc.StandardOutput.ReadToEndAsync();
                    var errorTask = proc.StandardError.ReadToEndAsync();
                    
                    using var cts = new CancellationTokenSource(timeout.Value);
                    try
                    {
                        await proc.WaitForExitAsync(cts.Token);
                        output = await outputTask;
                        error = await errorTask;
                    }
                    catch (OperationCanceledException)
                    {
                        try
                        {
                            proc.Kill(entireProcessTree: true);
                        }
                        catch (Exception killEx)
                        {
                            LogService.Instance.Warn($"Impossible de terminer PowerShell après timeout : {killEx.Message}", "SystemService");
                        }
                        return new CommandResult { Success = false, TimedOut = true, WasElevated = asAdmin };
                    }
                }
                else
                {
                    using var cts = new CancellationTokenSource(timeout.Value);
                    try
                    {
                        await proc.WaitForExitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        try
                        {
                            proc.Kill(entireProcessTree: true);
                        }
                        catch (Exception killEx)
                        {
                            LogService.Instance.Warn($"Impossible de terminer PowerShell admin après timeout : {killEx.Message}", "SystemService");
                        }
                        return new CommandResult { Success = false, TimedOut = true, WasElevated = true };
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
                return new CommandResult { Success = false, Error = "❌ Élévation refusée par l'utilisateur (UAC annulé).", WasElevated = true };
            }
            catch (Exception ex)
            {
                return new CommandResult { Success = false, Error = $"❌ Erreur PowerShell : {ex.Message}" };
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
                if (key == null) return $"❌ Impossible d'ouvrir la clé : {keyPath}";
                key.SetValue(valueName, value, kind);
                return $"✅ Registre : {valueName} = {value}";
            }
            catch (UnauthorizedAccessException)
            {
                return "❌ Droits insuffisants. Relancez InfoZen en administrateur.";
            }
            catch (Exception ex)
            {
                return $"❌ Erreur registre : {ex.Message}";
            }
        }

        public static string SetRegistryCurrentUser(string keyPath, string valueName, object value, RegistryValueKind kind)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(keyPath, writable: true);
                if (key == null) return $"❌ Clé introuvable : {keyPath}";
                key.SetValue(valueName, value, kind);
                return $"✅ Registre HKCU : {valueName} = {value}";
            }
            catch (Exception ex)
            {
                return $"❌ Erreur registre : {ex.Message}";
            }
        }

        public static string DeleteRegistryValue(string keyPath, string valueName)
        {
            try
            {
                using var key = Registry.LocalMachine.OpenSubKey(keyPath, writable: true);
                key?.DeleteValue(valueName, throwOnMissingValue: false);
                return $"✅ Valeur supprimée : {valueName}";
            }
            catch (Exception ex)
            {
                return $"❌ Erreur registre : {ex.Message}";
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
                info.OsName      = Environment.OSVersion.ToString();
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
        public static async Task<CommandResult> CreateRestorePointAsync(string description = "InfoZen – avant optimisation")
        {
            // Vérifier d'abord si on est admin
            if (!AdminChecker.IsRunningAsAdmin())
            {
                return new CommandResult
                {
                    Success = false,
                    Error = "❌ Droits administrateur requis pour créer un point de restauration. Relancez InfoZen en admin."
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
                result.Output = $"✅ Point de restauration créé : {label} (ID {sequence}).";
                result.Error = "";
            }
            else if (marker.StartsWith("WARN|", StringComparison.Ordinal))
            {
                result.Success = false;
                var parts = marker.Split('|');
                result.Error = parts.Length > 1
                    ? $"⚠ {parts[1]}"
                    : "⚠ Point de restauration non confirmé.";
                result.Output = "";
            }
            else if (marker.StartsWith("ERROR|", StringComparison.Ordinal))
            {
                result.Success = false;
                var parts = marker.Split('|');
                result.Error = parts.Length > 1 ? $"❌ {parts[1]}" : "❌ Échec de création du point de restauration.";
                result.Output = "";
            }
            else
            {
                result.Success = false;
                result.Error = string.IsNullOrWhiteSpace(marker)
                    ? "❌ Validation du point de restauration impossible (sortie vide)."
                    : $"❌ Validation du point de restauration impossible : {marker}";
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
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "InfoZen", "Logs");
                Directory.CreateDirectory(dir);
                string file = Path.Combine(dir, $"InfoZen_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(file, logContent, Encoding.UTF8);
                return file;
            }
            catch (Exception ex)
            {
                return $"❌ Export échoué : {ex.Message}";
            }
        }
    }
}
