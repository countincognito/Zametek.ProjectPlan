namespace Zametek.Data.ProjectPlan.v0_1_0
{
    [Serializable]
    public record ColorFormatModel
    {
        public byte A { get; init; }

        public byte R { get; init; }

        public byte G { get; init; }

        public byte B { get; init; }
    }
}
