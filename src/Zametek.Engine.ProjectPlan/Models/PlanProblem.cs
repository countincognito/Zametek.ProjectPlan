namespace Zametek.Engine.ProjectPlan
{
    // One problem that the check of a project found before its load: its place in the file, as a JSON pointer from the root
    // of the file (/Files/0/Scenario/ProjectStart), the code of the rule that it breaks, as the API names the codes
    // (required, say), and a phrase that continues from the place (is required).
    public record PlanProblem(
        string Pointer,
        string Code,
        string Detail);
}
