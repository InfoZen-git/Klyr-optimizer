using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace Klyr.Services
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string   Level     { get; set; } = "INFO";  // INFO | SUCCESS | ERROR | WARN
        public string   Message   { get; set; } = "";
        public string   Source    { get; set; } = "";

        public string Formatted =>
            $"[{Timestamp:HH:mm:ss}] [{Level}] {(string.IsNullOrEmpty(Source) ? "" : $"[{Source}] ")}{Message}";

        /// <summary>Variante datée pour fichier (avec date complète).</summary>
        public string FormattedForFile =>
            $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] {(string.IsNullOrEmpty(Source) ? "" : $"[{Source}] ")}{Message}";
    }

    /// <summary>
    /// Gère les logs du terminal et l'export en fichier .txt.
    /// FIX P1-04: Buffer circulaire limité à MaxEntries pour éviter croissance mémoire infinie.
    /// </summary>
    public class LogService : INotifyPropertyChanged
    {
        private static LogService? _instance;
        public static LogService Instance => _instance ??= new LogService();

        /// <summary>Nombre maximum d'entrées conservées en mémoire.</summary>
        public const int MaxEntries = 2000;

        public ObservableCollection<LogEntry> Entries { get; } = new();

        // FIX P1-04: StringBuilder remplacé par liste avec taille bornée
        private readonly List<string> _logLines = new();
        private string _cachedTerminalText = "";
        private bool _terminalTextDirty = true;

        // FIX P2-12: chemin du fichier auto-save (un fichier par jour)
        private static readonly string LogsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Klyr", "Logs");
        private static readonly object _fileLock = new();
        private static bool _autoSaveDisabled; // désactive après échec persistant pour ne pas spammer

        private static string GetTodaysLogPath() =>
            Path.Combine(LogsDir, $"Klyr_{DateTime.Now:yyyyMMdd}.log");

        public void Log(string message, string level = "INFO", string source = "")
        {
            var entry = new LogEntry { Message = message, Level = level, Source = source };
            var formattedLine = entry.Formatted;

            // FIX P2-12: append fichier hors UI thread (best effort, ne bloque jamais le caller)
            AppendToFileSafe(entry);

            // Dispatch vers UI thread si nécessaire
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                // FIX P1-04: Supprimer les anciennes entrées si on dépasse la limite
                while (Entries.Count >= MaxEntries)
                {
                    Entries.RemoveAt(0);
                }
                Entries.Add(entry);

                lock (_logLines)
                {
                    while (_logLines.Count >= MaxEntries)
                    {
                        _logLines.RemoveAt(0);
                    }
                    _logLines.Add(formattedLine);
                    _terminalTextDirty = true;
                }

                OnPropertyChanged(nameof(TerminalText));
            });
        }

        /// <summary>
        /// FIX P2-12 : append silencieux dans le fichier journalier.
        /// Ne lance jamais d'exception : si l'IO échoue, on désactive l'auto-save pour la session.
        /// Évite la récursion infinie avec SettingsService.Load() en lisant la propriété de manière protégée.
        /// </summary>
        private static void AppendToFileSafe(LogEntry entry)
        {
            if (_autoSaveDisabled) return;

            bool enabled;
            try
            {
                enabled = SettingsService.Current.AutoSaveLogs;
            }
            catch
            {
                // SettingsService pas encore prêt (early init) — on log quand même par défaut
                enabled = true;
            }
            if (!enabled) return;

            try
            {
                Directory.CreateDirectory(LogsDir);
                lock (_fileLock)
                {
                    File.AppendAllText(GetTodaysLogPath(),
                        entry.FormattedForFile + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // IO impossible (disque plein, perms, antivirus). On désactive pour la session.
                _autoSaveDisabled = true;
            }
        }

        public void Success(string msg, string source = "") => Log(msg, "SUCCESS", source);
        public void Error  (string msg, string source = "") => Log(msg, "ERROR",   source);
        public void Warn   (string msg, string source = "") => Log(msg, "WARN",    source);
        public void Info   (string msg, string source = "") => Log(msg, "INFO",    source);

        /// <summary>
        /// Texte complet du terminal. FIX P1-04: Cache le résultat pour éviter rebuilds répétés.
        /// </summary>
        public string TerminalText
        {
            get
            {
                lock (_logLines)
                {
                    if (_terminalTextDirty)
                    {
                        _cachedTerminalText = string.Join(Environment.NewLine, _logLines);
                        _terminalTextDirty = false;
                    }
                    return _cachedTerminalText;
                }
            }
        }

        public void Clear()
        {
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Entries.Clear();
                lock (_logLines)
                {
                    _logLines.Clear();
                    _cachedTerminalText = "";
                    _terminalTextDirty = false;
                }
                OnPropertyChanged(nameof(TerminalText));
            });
        }

        public string Export()
        {
            var header = new StringBuilder();
            header.AppendLine("════════════════════════════════════════════════════════");
            header.AppendLine($"   Klyr Optimiseur PC – Journal d'activité");
            header.AppendLine($"   Exporté le : {DateTime.Now:dd/MM/yyyy à HH:mm:ss}");
            header.AppendLine("════════════════════════════════════════════════════════");
            header.AppendLine();
            
            lock (_logLines)
            {
                header.Append(string.Join(Environment.NewLine, _logLines));
            }

            return SystemService.ExportLogs(header.ToString());
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
