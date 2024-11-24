using Avalonia.Platform;
using Avalonia.Styling;
using Semi.Avalonia;
using Zametek.Utility;

namespace Zametek.View.ProjectPlan
{
    public static class ThemeHelper
    {
        public static ThemeVariant GetThemeVariant(string? theme)
        {
            ThemeVariant themeVariant = ThemeVariant.Default;

            theme.ValueSwitchOn()
                .Case(Resource.ProjectPlan.Themes.Theme_Default, _ => { themeVariant = ThemeVariant.Default; })
                .Case(Resource.ProjectPlan.Themes.Theme_Light, _ => { themeVariant = ThemeVariant.Light; })
                .Case(Resource.ProjectPlan.Themes.Theme_Dark, _ => { themeVariant = ThemeVariant.Dark; })
                .Case(Resource.ProjectPlan.Themes.Theme_Aquatic, _ => { themeVariant = SemiTheme.Aquatic; })
                .Case(Resource.ProjectPlan.Themes.Theme_Desert, _ => { themeVariant = SemiTheme.Desert; })
                .Case(Resource.ProjectPlan.Themes.Theme_Dust, _ => { themeVariant = SemiTheme.Dust; })
                .Case(Resource.ProjectPlan.Themes.Theme_NightSky, _ => { themeVariant = SemiTheme.NightSky; });

            return themeVariant;
        }

        public static PlatformThemeVariant GetPlatformThemeVariant(string? theme)
        {
            return (PlatformThemeVariant?)GetThemeVariant(theme) ?? PlatformThemeVariant.Light;
        }

        public static ThemeVariant GetInheritedThemeVariant(string? theme)
        {
            return (ThemeVariant)GetPlatformThemeVariant(theme);
        }

    }
}
