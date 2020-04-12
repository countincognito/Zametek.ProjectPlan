using System;
using System.Collections.Generic;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    [Serializable]
    public class CoreState
    {
        public ArrowGraphSettingsModel ArrowGraphSettings { get; set; }

        public ResourceSettingsModel ResourceSettings { get; set; }

        public IEnumerable<DependentActivityModel> DependentActivities { get; set; }

        public DateTime ProjectStart { get; set; }

        public bool UseBusinessDays { get; set; }

        public bool ShowDates { get; set; }
    }
}
