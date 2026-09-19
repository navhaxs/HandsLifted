namespace HandsLiftedApp.Data.SlideTheme
{
    public static class SlideThemeTypeExtensions
    {
        public static bool AppliesToSongs(this BaseSlideTheme design) =>
            design.Type is SlideThemeType.General or SlideThemeType.SongTheme;

        public static bool AppliesToScripture(this BaseSlideTheme design) =>
            design.Type is SlideThemeType.General or SlideThemeType.ScriptureTheme;
    }
}
