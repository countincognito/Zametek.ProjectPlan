namespace Zametek.Common.ProjectPlan
{
    public static class DefaultFormatCollections
    {
        // Each read builds the lists afresh, so settings given the defaults own their copy: a change to one
        // project's formats can never reach another project's, or the defaults themselves.
        public static List<EdgeTypeFormatModel> EdgeTypeFormats =>
             [
                new()
                {
                    EdgeType = EdgeType.Activity,
                    EdgeDashStyle = EdgeDashStyle.Normal,
                    EdgeWeightStyle = EdgeWeightStyle.Normal
                },
                new()
                {
                    EdgeType = EdgeType.CriticalActivity,
                    EdgeDashStyle = EdgeDashStyle.Normal,
                    EdgeWeightStyle = EdgeWeightStyle.Bold
                },
                new()
                {
                    EdgeType = EdgeType.Dummy,
                    EdgeDashStyle = EdgeDashStyle.Dashed,
                    EdgeWeightStyle = EdgeWeightStyle.Normal
                },
                new()
                {
                    EdgeType = EdgeType.CriticalDummy,
                    EdgeDashStyle = EdgeDashStyle.Dashed,
                    EdgeWeightStyle = EdgeWeightStyle.Bold
                }
            ];

        public static List<NodeTypeFormatModel> NodeTypeFormats =>
            [
                new()
                {
                    NodeType = NodeType.Activity,
                    NodeBorderDashStyle = NodeBorderDashStyle.Normal,
                    NodeBorderWeightStyle = NodeBorderWeightStyle.Normal
                },
                new()
                {
                    NodeType = NodeType.CriticalActivity,
                    NodeBorderDashStyle = NodeBorderDashStyle.Normal,
                    NodeBorderWeightStyle = NodeBorderWeightStyle.Bold
                },
                new()
                {
                    NodeType = NodeType.Dummy,
                    NodeBorderDashStyle = NodeBorderDashStyle.Dashed,
                    NodeBorderWeightStyle = NodeBorderWeightStyle.Normal
                },
                new()
                {
                    NodeType = NodeType.CriticalDummy,
                    NodeBorderDashStyle = NodeBorderDashStyle.Dashed,
                    NodeBorderWeightStyle = NodeBorderWeightStyle.Normal
                }
            ];
    }
}
