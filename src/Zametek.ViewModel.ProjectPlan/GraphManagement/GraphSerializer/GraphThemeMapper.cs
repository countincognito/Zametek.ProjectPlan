using Zametek.Common.ProjectPlan;
using Zametek.Graphs.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // Maps the application's BaseTheme onto the Graphs library's own GraphTheme at the boundary, so
    // the library carries no dependency on the application's theme model.
    internal static class GraphThemeMapper
    {
        public static GraphTheme ToGraphTheme(this BaseTheme baseTheme)
        {
            return baseTheme == BaseTheme.Dark ? GraphTheme.Dark : GraphTheme.Light;
        }
    }
}
