namespace Zametek.Graphs.ProjectPlan
{
    // The dash style of a node border or edge in a DiagramGraphModel. A library-local equivalent of
    // the application's NodeBorderDashStyle/EdgeDashStyle, so the Graphs library carries no
    // dependency on the application's display models. The application maps its own dash styles onto
    // this when it builds the diagram.
    public enum GraphDashStyle
    {
        Normal,
        Dashed
    }
}
