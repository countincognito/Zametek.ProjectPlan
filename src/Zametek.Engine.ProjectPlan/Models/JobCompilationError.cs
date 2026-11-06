namespace Zametek.Engine.ProjectPlan
{
    // One thing the compiler found wrong with a plan: its code, as zpp prints it (P0010, say), and its message.
    public record JobCompilationError(
        string Code,
        string Message);
}
