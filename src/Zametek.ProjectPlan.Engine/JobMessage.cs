namespace Zametek.ProjectPlan.Engine
{
    // A message a job raised as it ran, with the parts the desktop's dialog would show.
    public record JobMessage(
        JobMessageKind Kind,
        string Title,
        string Header,
        string Message);
}
