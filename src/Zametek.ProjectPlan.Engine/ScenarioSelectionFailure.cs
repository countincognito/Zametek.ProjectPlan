namespace Zametek.ProjectPlan.Engine
{
    // Why a job could not select the scenario its request named.
    public enum ScenarioSelectionFailure
    {
        // No scenario has that name, id or id prefix.
        NoMatch,

        // More than one scenario has that name or id prefix.
        SeveralMatches,

        // The scenario exists, but the project file holds no data for it.
        NoScenarioData
    }
}
