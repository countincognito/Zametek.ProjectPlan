namespace Zametek.Graphs.Avalonia
{
    // The light/dark theme the graph controls and renderers draw against. A library-local
    // equivalent of the application's BaseTheme, so the Graphs library carries no dependency on the
    // application's domain models. The host view-model maps its own theme onto this at the binding
    // boundary (e.g. via BaseTheme.ToGraphTheme()).
    public enum GraphTheme
    {
        Light,
        Dark
    }
}
