using System.IO;
using Klyr.Models;

namespace Klyr.Services
{
    /// <summary>
    /// v2.3.0 — Disk Analyzer (inspiré de Kudu).
    /// Scanne un dossier, calcule la taille de chaque sous-dossier/fichier de premier niveau,
    /// trie par taille décroissante et calcule le % du parent.
    /// </summary>
    public static class DiskAnalyzerService
    {
        /// <summary>Racines proposées par défaut (lecteurs fixes).</summary>
        public static List<string> GetDriveRoots()
        {
            var roots = new List<string>();
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (d.IsReady && d.DriveType == DriveType.Fixed)
                        roots.Add(d.RootDirectory.FullName);
                }
            }
            catch { /* best-effort */ }
            return roots;
        }

        /// <summary>
        /// Analyse les enfants directs de <paramref name="path"/>.
        /// Le calcul de taille des dossiers est récursif mais parallélisé.
        /// </summary>
        public static List<DiskEntry> Analyze(string path, CancellationToken ct = default)
        {
            var entries = new List<DiskEntry>();
            if (!Directory.Exists(path)) return entries;

            string[] dirs = Array.Empty<string>();
            string[] files = Array.Empty<string>();
            try { dirs  = Directory.GetDirectories(path); } catch { }
            try { files = Directory.GetFiles(path); } catch { }

            // Sous-dossiers (taille récursive, en parallèle)
            var dirEntries = new List<DiskEntry>();
            System.Threading.Tasks.Parallel.ForEach(
                dirs,
                new System.Threading.Tasks.ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
                dir =>
                {
                    long size = GetDirectorySize(dir, ct);
                    lock (dirEntries)
                    {
                        dirEntries.Add(new DiskEntry
                        {
                            Name = Path.GetFileName(dir),
                            FullPath = dir,
                            SizeBytes = size,
                            IsDirectory = true
                        });
                    }
                });
            entries.AddRange(dirEntries);

            // Fichiers de premier niveau
            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var fi = new FileInfo(file);
                    entries.Add(new DiskEntry
                    {
                        Name = fi.Name,
                        FullPath = file,
                        SizeBytes = fi.Length,
                        IsDirectory = false
                    });
                }
                catch { }
            }

            long total = entries.Sum(e => e.SizeBytes);
            if (total > 0)
                foreach (var e in entries)
                    e.PercentOfParent = (double)e.SizeBytes / total * 100.0;

            return entries.OrderByDescending(e => e.SizeBytes).ToList();
        }

        private static long GetDirectorySize(string dir, CancellationToken ct)
        {
            long total = 0;
            try
            {
                var stack = new Stack<string>();
                stack.Push(dir);
                while (stack.Count > 0)
                {
                    if (ct.IsCancellationRequested) break;
                    string current = stack.Pop();

                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(current))
                        {
                            try { total += new FileInfo(f).Length; } catch { }
                        }
                        foreach (var sub in Directory.EnumerateDirectories(current))
                            stack.Push(sub);
                    }
                    catch { /* accès refusé sur ce sous-dossier */ }
                }
            }
            catch { }
            return total;
        }
    }
}
