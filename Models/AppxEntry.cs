using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Klyr.Models
{
    /// <summary>
    /// v2.5.0 — Application du Windows Store (UWP/Appx) listée dans le débloat avancé.
    /// </summary>
    public class AppxEntry : INotifyPropertyChanged
    {
        public string Name            { get; set; } = "";   // identité (ex. Microsoft.BingNews)
        public string PackageFullName { get; set; } = "";   // requis pour la désinstallation
        public string Publisher       { get; set; } = "";

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
