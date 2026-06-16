using System.Net.Http;
using System.Text.Json;

namespace Klyr.Services
{
    /// <summary>Infos sur une mise à jour disponible.</summary>
    public sealed class UpdateInfo
    {
        public string Version { get; set; } = "";   // ex. "2.4.0"
        public string Url     { get; set; } = "";    // page de la release GitHub
    }

    /// <summary>
    /// v2.3.0 — Vérification de mise à jour via l'API GitHub Releases.
    /// PRIVACY : simple GET vers api.github.com (aucune donnée utilisateur envoyée,
    /// pas de télémétrie). Désactivable via Settings.EnableUpdateCheck.
    ///
    /// ⚠️ À CONFIGURER : remplace GitHubOwner / GitHubRepo par ton dépôt réel.
    /// Tant que ce sont les placeholders, la vérification est désactivée (no-op sûr).
    /// </summary>
    public static class UpdateService
    {
        // TODO ⚠️ — mets ici ton vrai dépôt GitHub ex: ("Klyr")
        private const string GitHubOwner = "InfoZen-git";
        private const string GitHubRepo  = "Klyr-optimizer";

        private static bool IsConfigured =>
            GitHubOwner != "OWNER" && GitHubRepo != "REPO" &&
            !string.IsNullOrWhiteSpace(GitHubOwner) && !string.IsNullOrWhiteSpace(GitHubRepo);

        /// <summary>
        /// Renvoie une UpdateInfo si une version plus récente existe, sinon null.
        /// Best-effort : ne throw jamais (réseau coupé, repo non configuré, etc.).
        /// </summary>
        public static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            if (!IsConfigured) return null;
            if (!SettingsService.Current.EnableUpdateCheck) return null;

            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                // GitHub exige un User-Agent
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Klyr-Updater");
                client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

                string url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
                string json = await client.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string tag = root.TryGetProperty("tag_name", out var t) ? t.GetString() ?? "" : "";
                string htmlUrl = root.TryGetProperty("html_url", out var h) ? h.GetString() ?? "" : "";
                bool prerelease = root.TryGetProperty("prerelease", out var p) && p.GetBoolean();
                bool draft = root.TryGetProperty("draft", out var d) && d.GetBoolean();

                if (prerelease || draft) return null;

                var latest = ParseVersion(tag);
                var current = ParseVersion(DiagnosticService.AppVersion);
                if (latest == null || current == null) return null;

                if (latest > current)
                {
                    return new UpdateInfo
                    {
                        Version = $"{latest.Major}.{latest.Minor}.{latest.Build}",
                        Url     = string.IsNullOrWhiteSpace(htmlUrl)
                            ? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest"
                            : htmlUrl
                    };
                }
                return null;
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn($"Vérification de mise à jour échouée : {ex.Message}", "Système");
                return null;
            }
        }

        /// <summary>Parse "v2.4.0" / "2.4" → Version (tolérant).</summary>
        private static Version? ParseVersion(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim().TrimStart('v', 'V');
            // garde uniquement le segment numérique de tête (ex. "2.4.0-beta" → "2.4.0")
            int cut = s.IndexOfAny(new[] { '-', '+', ' ' });
            if (cut > 0) s = s.Substring(0, cut);
            // Version exige au moins major.minor
            if (!s.Contains('.')) s += ".0";
            return Version.TryParse(s, out var v) ? v : null;
        }
    }
}
