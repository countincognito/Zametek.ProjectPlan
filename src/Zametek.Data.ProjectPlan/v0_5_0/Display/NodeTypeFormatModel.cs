namespace Zametek.Data.ProjectPlan.v0_5_0
{
    [Serializable]
    public record NodeTypeFormatModel
    {
        public NodeType NodeType { get; init; }

        public NodeBorderDashStyle NodeBorderDashStyle { get; init; }

        public NodeBorderWeightStyle NodeBorderWeightStyle { get; init; }
    }
}
