﻿namespace Zametek.Data.ProjectPlan.v0_3_1
{
    [Serializable]
    public record ResourceSettingsModel
    {
        public List<ResourceModel> Resources { get; init; } = new List<ResourceModel>();

        public double DefaultUnitCost { get; init; }

        public bool AreDisabled { get; init; }
    }
}
