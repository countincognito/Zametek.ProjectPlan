namespace Zametek.ProjectPlan.CommandLine
{
    // The project as an output of a request. It has no settings yet, so it is asked for with an empty object - which can
    // later carry settings without a change that breaks a request.
    public record ProjectOptions;
}
