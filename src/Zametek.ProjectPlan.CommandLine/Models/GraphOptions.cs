namespace Zametek.ProjectPlan.CommandLine
{
    // A graph a job asks for: its format, as zpp's --*-format gives it.
    public record GraphOptions
    {
        public GraphExport Format { get; init; } = GraphExport.Jpeg;
    }
}
