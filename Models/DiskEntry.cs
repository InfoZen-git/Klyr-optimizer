namespace Klyr.Models
{
    /// <summary>
    /// v2.3.0 — Entrée du Disk Analyzer (dossier ou fichier) avec taille et % du parent.
    /// </summary>
    public class DiskEntry
    {
        public string Name      { get; set; } = "";
        public string FullPath  { get; set; } = "";
        public long   SizeBytes { get; set; }
        public bool   IsDirectory { get; set; }
        public double PercentOfParent { get; set; }

        public string SizeStr => SizeBytes switch
        {
            >= 1073741824 => $"{SizeBytes / 1073741824.0:F2} Go",
            >= 1048576    => $"{SizeBytes / 1048576.0:F1} Mo",
            >= 1024       => $"{SizeBytes / 1024.0:F0} Ko",
            _             => $"{SizeBytes} o"
        };

        public string PercentStr => $"{PercentOfParent:F1}%";

        /// <summary>Couleur de la barre : dégradé selon le poids.</summary>
        public string BarColor => PercentOfParent switch
        {
            >= 40 => "#E05252",
            >= 20 => "#E8A838",
            >= 8  => "#4B8BF5",
            _     => "#5A6478"
        };

        public string Icon => IsDirectory ? "" : ""; // Folder / Document (Fluent)
    }
}
