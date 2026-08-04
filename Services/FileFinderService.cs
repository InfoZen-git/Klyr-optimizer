using System.IO;
using System.Security.Cryptography;
using Klyr.Models;

namespace Klyr.Services
{
    /// <summary>
    /// v2.5.0 — Recherche de gros fichiers et de fichiers en double dans un dossier.
    /// Les doublons sont détectés par taille identique puis empreinte SHA-256 (contenu réel).
    /// La suppression passe par la Corbeille (réversible) via SafeDeleteService.
    /// </summary>
    public static class FileFinderService
    {
        private static readonly EnumerationOptions EnumOpts = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible    = true,
            AttributesToSkip      = FileAttributes.ReparsePoint | FileAttributes.System
        };

        /// <summary>Retourne les N plus gros fichiers du dossier, triés décroissant.</summary>
        public static List<FileEntry> FindLargeFiles(string root, int topN, CancellationToken ct)
        {
            var all = new List<FileEntry>();
            try
            {
                foreach (var path in Directory.EnumerateFiles(root, "*", EnumOpts))
                {
                    ct.ThrowIfCancellationRequested();
                    long size;
                    try { size = new FileInfo(path).Length; } catch { continue; }
                    all.Add(new FileEntry { Path = path, Size = size });
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { LogService.Instance.Warn($"Scan gros fichiers : {ex.Message}", "Système"); }

            return all.OrderByDescending(f => f.Size).Take(topN).ToList();
        }

        /// <summary>
        /// Retourne les fichiers en double (≥ minSizeBytes), aplatis et regroupés.
        /// Chaque groupe conserve la 1re copie non cochée (à garder) et coche les autres.
        /// </summary>
        public static List<FileEntry> FindDuplicates(string root, long minSizeBytes, CancellationToken ct)
        {
            // 1) Regrouper par taille (candidats)
            var bySize = new Dictionary<long, List<string>>();
            try
            {
                foreach (var path in Directory.EnumerateFiles(root, "*", EnumOpts))
                {
                    ct.ThrowIfCancellationRequested();
                    long size;
                    try { size = new FileInfo(path).Length; } catch { continue; }
                    if (size < minSizeBytes) continue;
                    if (!bySize.TryGetValue(size, out var l)) { l = new List<string>(); bySize[size] = l; }
                    l.Add(path);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { LogService.Instance.Warn($"Scan doublons : {ex.Message}", "Système"); }

            // 2) Pour chaque taille avec ≥2 fichiers, comparer par hash
            var result = new List<FileEntry>();
            int group = 0;
            foreach (var kv in bySize)
            {
                if (kv.Value.Count < 2) continue;
                ct.ThrowIfCancellationRequested();

                var byHash = new Dictionary<string, List<string>>();
                foreach (var path in kv.Value)
                {
                    ct.ThrowIfCancellationRequested();
                    string? hash = TryHash(path);
                    if (hash == null) continue;
                    if (!byHash.TryGetValue(hash, out var l)) { l = new List<string>(); byHash[hash] = l; }
                    l.Add(path);
                }

                foreach (var dup in byHash.Values)
                {
                    if (dup.Count < 2) continue;
                    group++;
                    string label = string.Format(Resources.Strings.Files_GroupLabel, group);
                    bool first = true;
                    foreach (var path in dup)
                    {
                        result.Add(new FileEntry
                        {
                            Path       = path,
                            Size       = kv.Key,
                            GroupLabel = label,
                            IsSelected = !first   // garde la 1re copie, coche les suivantes
                        });
                        first = false;
                    }
                }
            }

            return result;
        }

        private static string? TryHash(string path)
        {
            try
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(path);
                return Convert.ToHexString(sha.ComputeHash(stream));
            }
            catch { return null; }
        }
    }
}
