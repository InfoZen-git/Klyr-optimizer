namespace Klyr.Models
{
    public enum HealthSeverity { Good, Warning, Critical }

    /// <summary>
    /// v2.5.0 — Une recommandation du rapport de santé, avec une action facultative
    /// (navigation vers l'outil/module qui règle le problème).
    /// </summary>
    public class HealthCheck
    {
        public string         Title       { get; set; } = "";
        public string         Detail      { get; set; } = "";
        public HealthSeverity Severity    { get; set; } = HealthSeverity.Good;
        /// <summary>Page vers laquelle naviguer pour corriger (vide = pas d'action).</summary>
        public string         ActionPage  { get; set; } = "";

        public bool   HasAction     => !string.IsNullOrEmpty(ActionPage);
        public string SeverityColor => Severity switch
        {
            HealthSeverity.Critical => "#FF5252",
            HealthSeverity.Warning  => "#FFB300",
            _                       => "#4CAF50"
        };
    }
}
