using Microsoft.Win32;

namespace Klyr.Services
{
    /// <summary>
    /// v2.4.0 — Lancement de Klyr au démarrage de Windows via la clé Run HKCU
    /// (n'exige pas les droits administrateur). Best-effort, ne throw jamais.
    /// </summary>
    public static class AutoStartService
    {
        private const string RunKey    = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "Klyr";

        public static void Apply(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                if (key == null) return;

                if (enable)
                {
                    string exe = Environment.ProcessPath ?? "";
                    if (!string.IsNullOrEmpty(exe))
                        key.SetValue(ValueName, $"\"{exe}\"", RegistryValueKind.String);
                }
                else
                {
                    key.DeleteValue(ValueName, throwOnMissingValue: false);
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Réglage du démarrage automatique échoué : {ex.Message}", "Système");
            }
        }

        public static bool IsEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
                return key?.GetValue(ValueName) != null;
            }
            catch { return false; }
        }
    }
}
