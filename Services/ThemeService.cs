using System.Windows;
using System.Windows.Media;

namespace Klyr.Services
{
    /// <summary>
    /// Applique les palettes de couleurs globales de l'application.
    /// </summary>
    public static class ThemeService
    {
        private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        private static readonly IReadOnlyDictionary<string, Color> DarkPalette = new Dictionary<string, Color>
        {
            // Fonds
            ["BackgroundDeepBrush"]      = C("#141414"),
            ["BackgroundPanelBrush"]     = C("#1C1C1C"),
            ["BackgroundCardBrush"]      = C("#242424"),
            ["BackgroundHoverBrush"]     = C("#2A2A2A"),
            ["BackgroundFooterBrush"]    = C("#181818"),
            ["BackgroundPressedBrush"]   = C("#333333"),

            // Accent
            ["AccentBrush"]              = C("#4B8BF5"),
            ["AccentHoverBrush"]         = C("#6BA0F7"),
            ["AccentPressedBrush"]       = C("#3A72D4"),
            ["AccentSubtleBrush"]        = C("#1E2E4A"),

            // Sémantique
            ["SuccessBrush"]             = C("#4CAF50"),
            ["WarningBrush"]             = C("#E8A838"),
            ["ErrorBrush"]               = C("#E05252"),
            ["InfoBrush"]                = C("#4B8BF5"),

            // Texte
            ["TextPrimaryBrush"]         = C("#E8E8E8"),
            ["TextSecondaryBrush"]       = C("#8A8A8A"),
            ["TextMutedBrush"]           = C("#484848"),
            ["TextTertiaryBrush"]        = C("#606060"),
            ["TextEmphasizedBrush"]      = C("#C8C8C8"),
            ["TextDescriptionBrush"]     = C("#6A6A6A"),
            ["TextOnAccentBrush"]        = C("#FFFFFF"),

            // Bordures
            ["BorderBrush"]              = C("#2E2E2E"),
            ["BorderFocusBrush"]         = C("#4B8BF5"),
            ["BorderLightBrush"]         = C("#3A3A3A"),

            // Terminal
            ["TerminalBgBrush"]          = C("#0F0F0F"),
            ["TerminalTextBrush"]        = C("#A8C878"),

            // Sections status
            ["StatusSuccessSubtleBgBrush"]   = C("#1E241E"),
            ["StatusInfoSubtleBgBrush"]      = C("#1A1E28"),
            ["StatusWarningSubtleBgBrush"]   = C("#221E18"),
            ["StatusSuccessBorderBrush"]     = C("#2E3E2E"),
            ["StatusInfoBorderBrush"]        = C("#2A3040"),
            ["StatusWarningBorderBrush"]     = C("#3A3020"),
            ["StatusLiveSuccessBgBrush"]     = C("#1E2E1E"),
            ["StatusLiveSuccessBorderBrush"] = C("#2E4A2E"),

            // Badges
            ["BadgeAdminBgBrush"]        = C("#1E2834"),
            ["BadgeAdminTextBrush"]      = C("#6A9FD8"),
            ["BadgeRebootBgBrush"]       = C("#2A1E1E"),
            ["BadgeRebootTextBrush"]     = C("#C06060"),
            ["BadgeAdvancedBgBrush"]     = C("#3A2A1E"),
            ["BadgeAdvancedTextBrush"]   = C("#D0A46A"),
            ["BadgeSecurityBgBrush"]     = C("#3A1F2A"),
            ["BadgeSecurityTextBrush"]   = C("#D07AA0"),

            // Contrôles
            ["ScrollBarThumbBrush"]      = C("#383838"),
            ["ScrollBarThumbHoverBrush"] = C("#505050"),
            ["ToggleThumbBrush"]         = C("#606060")
        };

        private static readonly IReadOnlyDictionary<string, Color> LightPalette = new Dictionary<string, Color>
        {
            // Fonds
            ["BackgroundDeepBrush"]      = C("#F2F4F8"),
            ["BackgroundPanelBrush"]     = C("#FFFFFF"),
            ["BackgroundCardBrush"]      = C("#F7F9FC"),
            ["BackgroundHoverBrush"]     = C("#E9EDF5"),
            ["BackgroundFooterBrush"]    = C("#EDF0F5"),
            ["BackgroundPressedBrush"]   = C("#D8DEE7"),

            // Accent
            ["AccentBrush"]              = C("#2F6FEB"),
            ["AccentHoverBrush"]         = C("#4B82EE"),
            ["AccentPressedBrush"]       = C("#1F5BD6"),
            ["AccentSubtleBrush"]        = C("#DCE7FF"),

            // Sémantique
            ["SuccessBrush"]             = C("#2E7D32"),
            ["WarningBrush"]             = C("#C97E1F"),
            ["ErrorBrush"]               = C("#C62828"),
            ["InfoBrush"]                = C("#2F6FEB"),

            // Texte
            ["TextPrimaryBrush"]         = C("#1F2937"),
            ["TextSecondaryBrush"]       = C("#4B5563"),
            ["TextMutedBrush"]           = C("#6B7280"),
            ["TextTertiaryBrush"]        = C("#9CA3AF"),
            ["TextEmphasizedBrush"]      = C("#111827"),
            ["TextDescriptionBrush"]     = C("#6B7280"),
            ["TextOnAccentBrush"]        = C("#FFFFFF"),

            // Bordures
            ["BorderBrush"]              = C("#D1D5DB"),
            ["BorderFocusBrush"]         = C("#2F6FEB"),
            ["BorderLightBrush"]         = C("#E5E7EB"),

            // Terminal (volontairement sombre pour cohérence dev tools)
            ["TerminalBgBrush"]          = C("#111827"),
            ["TerminalTextBrush"]        = C("#BBF7D0"),

            // Sections status
            ["StatusSuccessSubtleBgBrush"]   = C("#E8F5E9"),
            ["StatusInfoSubtleBgBrush"]      = C("#E3F2FD"),
            ["StatusWarningSubtleBgBrush"]   = C("#FFF3E0"),
            ["StatusSuccessBorderBrush"]     = C("#A5D6A7"),
            ["StatusInfoBorderBrush"]        = C("#90CAF9"),
            ["StatusWarningBorderBrush"]     = C("#FFCC80"),
            ["StatusLiveSuccessBgBrush"]     = C("#E8F5E9"),
            ["StatusLiveSuccessBorderBrush"] = C("#81C784"),

            // Badges
            ["BadgeAdminBgBrush"]        = C("#E3F2FD"),
            ["BadgeAdminTextBrush"]      = C("#1565C0"),
            ["BadgeRebootBgBrush"]       = C("#FFEBEE"),
            ["BadgeRebootTextBrush"]     = C("#C62828"),
            ["BadgeAdvancedBgBrush"]     = C("#FFF3E0"),
            ["BadgeAdvancedTextBrush"]   = C("#E65100"),
            ["BadgeSecurityBgBrush"]     = C("#FCE4EC"),
            ["BadgeSecurityTextBrush"]   = C("#AD1457"),

            // Contrôles
            ["ScrollBarThumbBrush"]      = C("#C1C1C1"),
            ["ScrollBarThumbHoverBrush"] = C("#A1A1A1"),
            ["ToggleThumbBrush"]         = C("#9CA3AF")
        };

        public static void ApplyTheme(string? theme)
        {
            bool isLight = string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);
            var palette = isLight ? LightPalette : DarkPalette;

            foreach (var (key, color) in palette)
                SetBrushColor(key, color);

            SettingsService.Current.Theme = isLight ? "Light" : "Dark";
        }

        private static void SetBrushColor(string key, Color color)
        {
            if (Application.Current.Resources[key] is SolidColorBrush brush)
            {
                if (brush.IsFrozen)
                {
                    // Les Freezable peuvent être figés par WPF (read-only) : remplacer l'instance.
                    Application.Current.Resources[key] = new SolidColorBrush(color);
                    return;
                }

                brush.Color = color;
                return;
            }

            Application.Current.Resources[key] = new SolidColorBrush(color);
        }
    }
}
