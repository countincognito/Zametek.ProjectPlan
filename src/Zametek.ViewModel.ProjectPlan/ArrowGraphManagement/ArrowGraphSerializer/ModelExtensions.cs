using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public static class ModelExtensions
    {
        public static bool IsDummy(this ActivityModel activityModel)//!!)
        {
            return activityModel.Duration == 0;
        }
        public static bool IsCritical(this ActivityModel activityModel)//!!)
        {
            return activityModel.TotalSlack == 0;
        }
    }
}
