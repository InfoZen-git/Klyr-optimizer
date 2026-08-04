using Microsoft.VisualBasic.FileIO;

namespace Klyr.Services
{
    /// <summary>
    /// v2.5.0 — Suppression réversible : envoie les fichiers vers la Corbeille Windows
    /// (jamais de suppression définitive). Best-effort, ne throw jamais.
    /// </summary>
    public static class SafeDeleteService
    {
        public static bool ToRecycleBin(string path)
        {
            try
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Envoi à la corbeille échoué ({path}) : {ex.Message}", "Système");
                return false;
            }
        }
    }
}
