using System;
using System.Collections.Generic;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class CoreStateModel
    {
        public ArrowGraphSettingsModel ArrowGraphSettings { get; set; }

        public ResourceSettingsModel ResourceSettings { get; set; }

        public IEnumerable<DependentActivityModel> DependentActivities { get; set; }

        public DateTime ProjectStart { get; set; }

        public bool UseBusinessDays { get; set; }

        public bool ShowDates { get; set; }
    }
}
