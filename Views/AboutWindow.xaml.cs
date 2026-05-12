using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Klyr.Services;

namespace Klyr.Views
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            VersionText.Text = DiagnosticService.AppVersion;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) DragMove();
        }

        private void CopySystemInfo_Click(object sender, RoutedEventArgs e)
        {
            if (DiagnosticService.CopySystemInfoToClipboard())
            {
                MessageBox.Show(
                    "Infos système copiées dans le presse-papier.",
                    "Copié – Klyr",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "Impossible de copier dans le presse-papier.",
                    "Erreur – Klyr",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportReport_Click(object sender, RoutedEventArgs e)
        {
            string? zipPath = DiagnosticService.ExportDiagnosticReport();
            if (string.IsNullOrEmpty(zipPath) || !File.Exists(zipPath))
            {
                MessageBox.Show(
                    "Génération du rapport échouée.\nConsulte le terminal pour plus de détails.",
                    "Erreur – Klyr",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            var result = MessageBox.Show(
                $"Rapport diagnostic créé :\n\n{zipPath}\n\nOuvrir le dossier ?",
                "Rapport prêt – Klyr",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"/select,\"{zipPath}\"",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    // L'utilisateur peut toujours ouvrir manuellement le dossier
                }
            }
        }
    }
}
