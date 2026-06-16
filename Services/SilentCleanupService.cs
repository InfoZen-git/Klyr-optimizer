using System.IO;

namespace Klyr.Services
{
    /// <summary>
    /// v2.3.0 — Nettoyage silencieux (headless) déclenché par le scan programmé
    /// (Klyr.exe --scheduled-clean). Sûr et sans admin : %TEMP% utilisateur + caches navigateurs.
    /// Retourne l'espace libéré, puis l'app se ferme.
    /// </summary>
    public static class SilentCleanupService
    {
        public static long Run()
        {
            long freed = 0;

            // 1) %TEMP% utilisateur
            freed += CleanTempFolder(Path.GetTempPath());

            // 2) Caches navigateurs (cache uniquement, pas cookies/historique)
            try
            {
                var browsers = BrowserCleanerService.DetectBrowsers();
                foreach (var b in browsers)
                {
                    b.CleanCache = true;
                    b.CleanCookies = false;
                    b.CleanHistory = false;
                }
                freed += BrowserCleanerService.Clean(browsers);
            }
            catch { /* best-effort */ }

            return freed;
        }

        private static long CleanTempFolder(string temp)
        {
            long freed = 0;
            try
            {
                if (!Directory.Exists(temp)) return 0;
                foreach (var file in Directory.EnumerateFiles(temp))
                {
                    try { long s = new FileInfo(file).Length; File.Delete(file); freed += s; }
                    catch { /* verrouillé */ }
                }
                foreach (var dir in Directory.EnumerateDirectories(temp))
                {
                    try
                    {
                        long s = DirSize(dir);
                        Directory.Delete(dir, recursive: true);
                        freed += s;
                    }
                    catch { }
                }
            }
            catch { }
            return freed;
        }

        private static long DirSize(string dir)
        {
            try { return new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length); }
            catch { return 0; }
        }
    }
}
