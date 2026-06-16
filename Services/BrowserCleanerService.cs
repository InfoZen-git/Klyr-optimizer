using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Services
{
    /// <summary>
    /// v2.3.0 — Cible de nettoyage navigateur (cache / cookies / historique).
    /// </summary>
    public class BrowserTarget : INotifyPropertyChanged
    {
        public string BrowserName { get; set; } = "";
        public string Glyph       { get; set; } = "";
        /// <summary>Chemins de cache (vidés si CleanCache).</summary>
        public List<string> CachePaths   { get; set; } = new();
        /// <summary>Fichiers cookies (supprimés si CleanCookies).</summary>
        public List<string> CookiePaths  { get; set; } = new();
        /// <summary>Fichiers historique (supprimés si CleanHistory).</summary>
        public List<string> HistoryPaths { get; set; } = new();

        public bool IsInstalled { get; set; }

        private bool _cleanCache = true;
        private bool _cleanCookies;
        private bool _cleanHistory;
        public bool CleanCache   { get => _cleanCache;   set { _cleanCache = value; OnPropertyChanged(); } }
        public bool CleanCookies { get => _cleanCookies; set { _cleanCookies = value; OnPropertyChanged(); } }
        public bool CleanHistory { get => _cleanHistory; set { _cleanHistory = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    /// <summary>
    /// v2.3.0 — Browser Cleaner (inspiré de Kudu).
    /// Détecte Chrome, Edge, Firefox, Brave et nettoie cache/cookies/historique.
    /// </summary>
    public static class BrowserCleanerService
    {
        public static List<BrowserTarget> DetectBrowsers()
        {
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

            var targets = new List<BrowserTarget>
            {
                BuildChromium("Google Chrome", Path.Combine(local, "Google", "Chrome", "User Data", "Default")),
                BuildChromium("Microsoft Edge", Path.Combine(local, "Microsoft", "Edge", "User Data", "Default")),
                BuildChromium("Brave", Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data", "Default")),
                BuildFirefox(roaming)
            };

            return targets.Where(t => t.IsInstalled).ToList();
        }

        private static BrowserTarget BuildChromium(string name, string profileDir)
        {
            var t = new BrowserTarget { BrowserName = name, Glyph = "" };
            if (!Directory.Exists(profileDir)) { t.IsInstalled = false; return t; }
            t.IsInstalled = true;

            // Cache : dossiers Cache, Code Cache, GPUCache
            t.CachePaths.Add(Path.Combine(profileDir, "Cache"));
            t.CachePaths.Add(Path.Combine(profileDir, "Code Cache"));
            t.CachePaths.Add(Path.Combine(profileDir, "GPUCache"));
            t.CachePaths.Add(Path.Combine(profileDir, "Service Worker", "CacheStorage"));

            // Cookies (fichier SQLite)
            t.CookiePaths.Add(Path.Combine(profileDir, "Network", "Cookies"));
            t.CookiePaths.Add(Path.Combine(profileDir, "Cookies"));

            // Historique
            t.HistoryPaths.Add(Path.Combine(profileDir, "History"));
            return t;
        }

        private static BrowserTarget BuildFirefox(string roaming)
        {
            var t = new BrowserTarget { BrowserName = "Mozilla Firefox", Glyph = "" };
            string profilesRoot = Path.Combine(roaming, "Mozilla", "Firefox", "Profiles");
            if (!Directory.Exists(profilesRoot)) { t.IsInstalled = false; return t; }

            var profiles = Directory.GetDirectories(profilesRoot);
            if (profiles.Length == 0) { t.IsInstalled = false; return t; }
            t.IsInstalled = true;

            string localProfiles = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Mozilla", "Firefox", "Profiles");

            foreach (var prof in profiles)
            {
                string profName = Path.GetFileName(prof);
                // Cache (sous LocalAppData)
                t.CachePaths.Add(Path.Combine(localProfiles, profName, "cache2"));
                // Cookies + historique (places.sqlite contient l'historique)
                t.CookiePaths.Add(Path.Combine(prof, "cookies.sqlite"));
                t.HistoryPaths.Add(Path.Combine(prof, "places.sqlite"));
            }
            return t;
        }

        /// <summary>
        /// Nettoie les cibles sélectionnées. Retourne le total d'octets libérés.
        /// </summary>
        public static long Clean(IEnumerable<BrowserTarget> targets)
        {
            long freed = 0;
            foreach (var t in targets)
            {
                if (t.CleanCache)   freed += CleanPaths(t.CachePaths, isDir: true);
                if (t.CleanCookies) freed += CleanPaths(t.CookiePaths, isDir: false);
                if (t.CleanHistory) freed += CleanPaths(t.HistoryPaths, isDir: false);
            }
            return freed;
        }

        private static long CleanPaths(IEnumerable<string> paths, bool isDir)
        {
            long freed = 0;
            foreach (var path in paths)
            {
                try
                {
                    if (isDir)
                    {
                        if (!Directory.Exists(path)) continue;
                        freed += DirSize(path);
                        Directory.Delete(path, recursive: true);
                    }
                    else
                    {
                        if (!File.Exists(path)) continue;
                        freed += new FileInfo(path).Length;
                        File.Delete(path);
                    }
                }
                catch { /* fichier verrouillé (navigateur ouvert) */ }
            }
            return freed;
        }

        private static long DirSize(string dir)
        {
            try
            {
                return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
            }
            catch { return 0; }
        }
    }
}
