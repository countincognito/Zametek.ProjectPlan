namespace Zametek.Common.ProjectPlan
{
    // A persisted interactive-graph layout: the resolved on-screen position of each node, in layout
    // space (the control library re-applies its own scaling/margin). Stored per graph (arrow / vertex)
    // on a scenario so a dragged arrangement can be rehydrated on load. Positions are matched back to
    // nodes by Id (best-effort: ids no longer present are dropped, nodes without a stored position are
    // auto-laid-out), so only the coordinates are stored - node sizes, edges and presentation are
    // recomputed from the domain graph.
    [Serializable]
    public record GraphLayoutModel
    {
        public List<NodeLayoutModel> Nodes { get; init; } = [];
    }
}
