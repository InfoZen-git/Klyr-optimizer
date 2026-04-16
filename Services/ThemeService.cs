using System.Windows;
using System.Windows.Media;

namespace InfoZen.Services
{
    /// <summary>
    /// Applique les palettes de couleurs globales de l'application.
    /// </summary>
    public static class ThemeService
    {
        private static readonly IReadOnlyDictionary<string, Color> DarkPalette = new Dictionary<string, Color>
        {
            ["BackgroundDeepBrush"] = (Color)ColorConverter.ConvertFromString("#141414"),
            ["BackgroundPanelBrush"] = (Color)ColorConverter.ConvertFromString("#1C1C1C"),
            ["BackgroundCardBrush"] = (Color)ColorConverter.ConvertFromString("#242424"),
            ["BackgroundHoverBrush"] = (Color)ColorConverter.ConvertFromString("#2A2A2A"),
            ["AccentBrush"] = (Color)ColorConverter.ConvertFromString("#4B8BF5"),
            ["AccentHoverBrush"] = (Color)ColorConverter.ConvertFromString("#6BA0F7"),
            ["AccentPressedBrush"] = (Color)ColorConverter.ConvertFromString("#3A72D4"),
            ["AccentSubtleBrush"] = (Color)ColorConverter.ConvertFromString("#1E2E4A"),
            ["TextPrimaryBrush"] = (Color)ColorConverter.ConvertFromString("#E8E8E8"),
            ["TextSecondaryBrush"] = (Color)ColorConverter.ConvertFromString("#8A8A8A"),
            ["TextMutedBrush"] = (Color)ColorConverter.ConvertFromString("#484848"),
            ["BorderBrush"] = (Color)ColorConverter.ConvertFromString("#2E2E2E"),
            ["BorderFocusBrush"] = (Color)ColorConverter.ConvertFromString("#4B8BF5"),
            ["TerminalBgBrush"] = (Color)ColorConverter.ConvertFromString("#0F0F0F"),
            ["TerminalTextBrush"] = (Color)ColorConverter.ConvertFromString("#A8C878")
        };

        private static readonly IReadOnlyDictionary<string, Color> LightPalette = new Dictionary<string, Color>
        {
            ["BackgroundDeepBrush"] = (Color)ColorConverter.ConvertFromString("#F2F4F8"),
            ["BackgroundPanelBrush"] = (Color)ColorConverter.ConvertFromString("#FFFFFF"),
            ["BackgroundCardBrush"] = (Color)ColorConverter.ConvertFromString("#F7F9FC"),
            ["BackgroundHoverBrush"] = (Color)ColorConverter.ConvertFromString("#E9EDF5"),
            ["AccentBrush"] = (Color)ColorConverter.ConvertFromString("#2F6FEB"),
            ["AccentHoverBrush"] = (Color)ColorConverter.ConvertFromString("#4B82EE"),
            ["AccentPressedBrush"] = (Color)ColorConverter.ConvertFromString("#1F5BD6"),
            ["AccentSubtleBrush"] = (Color)ColorConverter.ConvertFromString("#DCE7FF"),
            ["TextPrimaryBrush"] = (Color)ColorConverter.ConvertFromString("#1F2937"),
            ["TextSecondaryBrush"] = (Color)ColorConverter.ConvertFromString("#4B5563"),
            ["TextMutedBrush"] = (Color)ColorConverter.ConvertFromString("#6B7280"),
            ["BorderBrush"] = (Color)ColorConverter.ConvertFromString("#D1D5DB"),
            ["BorderFocusBrush"] = (Color)ColorConverter.ConvertFromString("#2F6FEB"),
            ["TerminalBgBrush"] = (Color)ColorConverter.ConvertFromString("#111827"),
            ["TerminalTextBrush"] = (Color)ColorConverter.ConvertFromString("#BBF7D0")
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
