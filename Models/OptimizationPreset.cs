namespace Klyr.Models
{
    /// <summary>
    /// v2.5.0 — Profil 1-clic : un ensemble curé d'optimisations (par ID) appliqué
    /// en une fois à travers plusieurs modules.
    /// </summary>
    public sealed class OptimizationPreset
    {
        public string   Id               { get; init; } = "";
        public string   Name             { get; init; } = "";
        public string   Description      { get; init; } = "";
        public string[] OptimizationIds  { get; init; } = System.Array.Empty<string>();

        /// <summary>Nombre d'optims déclarées dans le profil (pour l'affichage).</summary>
        public int Count => OptimizationIds.Length;

        /// <summary>Nombre d'optims du profil réellement disponibles (rempli à la résolution).</summary>
        public int ResolvedCount { get; set; }
    }
}
