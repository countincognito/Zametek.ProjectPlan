namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record NodeTypeFormatModel
    {
        public NodeType NodeType { get; init; }

        public NodeBorderDashStyle NodeBorderDashStyle { get; init; }

        public NodeBorderWeightStyle NodeBorderWeightStyle { get; init; }
    }
}
