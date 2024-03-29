﻿namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public record TrackerModel
    {
        public int Index { get; init; }

        public int Time { get; init; }

        public int ActivityId { get; init; }

        public bool IsIncluded { get; init; }

        public int PercentageComplete { get; init; }
    }
}
