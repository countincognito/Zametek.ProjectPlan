namespace Zametek.Data.ProjectPlan.v0_6_2
{
    // The headcount of the schedule: the peak, the average and their ratio. It is new in v0.6.2. A file from before it has
    // none, and the first compile works it out.
    [Serializable]
    public record HeadcountModel
    {
        public int? Peak { get; init; }

        public double? Average { get; init; }

        public double? PeakToAverageRatio { get; init; }
    }
}
