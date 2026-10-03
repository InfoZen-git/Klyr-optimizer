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

        /// <summary>
        /// Âge minimal d'un élément de %TEMP% avant suppression : les fichiers plus récents peuvent
        /// être utilisés par un installeur ou une application en cours d'exécution.
        /// </summary>
        private static readonly TimeSpan MinTempAge = TimeSpan.FromHours(24);

        private static long CleanTempFolder(string temp)
        {
            try
            {
                if (!Directory.Exists(temp)) return 0;
                return CleanDirectoryContents(new DirectoryInfo(temp), DateTime.UtcNow - MinTempAge);
            }
            catch { return 0; }
        }

        /// <summary>
        /// Supprime les fichiers dont la dernière modification/accès est antérieure à
        /// <paramref name="cutoffUtc"/>, puis les sous-dossiers devenus vides. Les points de reparse
        /// (jonctions, liens symboliques) ne sont jamais suivis ni supprimés.
        /// </summary>
        private static long CleanDirectoryContents(DirectoryInfo dir, DateTime cutoffUtc)
        {
            long freed = 0;

            foreach (var file in dir.EnumerateFiles())
            {
                try
                {
                    if (file.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    if (file.LastWriteTimeUtc > cutoffUtc || file.LastAccessTimeUtc > cutoffUtc) continue;
                    long s = file.Length;
                    file.Delete();
                    freed += s;
                }
                catch { /* verrouillé */ }
            }

            foreach (var sub in dir.EnumerateDirectories())
            {
                try
                {
                    if (sub.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    freed += CleanDirectoryContents(sub, cutoffUtc);

                    if (sub.LastWriteTimeUtc <= cutoffUtc && !sub.EnumerateFileSystemInfos().Any())
                        sub.Delete();
                }
                catch { /* verrouillé ou accès refusé */ }
            }

            return freed;
        }
    }
}
