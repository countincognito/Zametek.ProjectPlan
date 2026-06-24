namespace Zametek.Data.ProjectPlan.v0_6_1
{
    [Serializable]
    public record NodeLayoutModel
    {
        public int Id { get; init; }

        public double X { get; init; }

        public double Y { get; init; }
    }
}
