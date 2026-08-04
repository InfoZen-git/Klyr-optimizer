using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace Klyr.Models
{
    /// <summary>
    /// v2.5.0 — Fichier trouvé par l'outil Gros fichiers &amp; doublons.
    /// </summary>
    public class FileEntry : INotifyPropertyChanged
    {
        public string Path      { get; set; } = "";
        public long   Size      { get; set; }
        /// <summary>Étiquette de groupe pour les doublons (vide pour les gros fichiers).</summary>
        public string GroupLabel { get; set; } = "";

        public string Name => System.IO.Path.GetFileName(Path);

        public string SizeStr => Size switch
        {
            >= 1073741824 => $"{Size / 1073741824.0:F2} Go",
            >= 1048576    => $"{Size / 1048576.0:F1} Mo",
            >= 1024       => $"{Size / 1024.0:F0} Ko",
            _             => $"{Size} o"
        };

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
