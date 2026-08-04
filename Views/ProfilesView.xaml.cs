using System.Windows.Controls;

namespace Klyr.Views
{
    /// <summary>
    /// v2.5.0 — Profils 1-clic. Le DataContext (MainViewModel) est hérité de la fenêtre :
    /// la vue se contente d'afficher les presets et de déclencher RunPresetCommand.
    /// </summary>
    public partial class ProfilesView : UserControl
    {
        public ProfilesView()
        {
            InitializeComponent();
        }
    }
}
