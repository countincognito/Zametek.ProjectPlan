using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ConstraintsValidationRule
        : ValidationRule
    {
        public override ValidationResult Validate(object value, CultureInfo culture)
        {
            var bindingGroup = value as BindingGroup;

            if (bindingGroup != null)
            {
                // Loop through binding sources - could be multiple.

                foreach (var bindingSource in bindingGroup.Items)
                {
                    var managedActivityViewModel = bindingSource as ManagedActivityViewModel;

                    if (managedActivityViewModel != null)
                    {
                        if (managedActivityViewModel.MinimumFreeSlack.HasValue
                            && managedActivityViewModel.MaximumLatestFinishTime.HasValue)
                        {
                            return new ValidationResult(false, Resource.ProjectPlan.Resources.Label_CannotSetMinimumFreeSlackAndMaximumLatestFinishTimeAtSameTime);
                        }

                        if (managedActivityViewModel.MinimumEarliestStartTime.HasValue
                            && managedActivityViewModel.MaximumLatestFinishTime.HasValue
                            && (managedActivityViewModel.MaximumLatestFinishTime.Value - managedActivityViewModel.MinimumEarliestStartTime.Value) < managedActivityViewModel.Duration)
                        {
                            return new ValidationResult(false, Resource.ProjectPlan.Resources.Label_MinimumEarliestStartTimeToMaximumLatestFinishTimeMustBeGreaterThanOrEqualToDuration);
                        }
                    }
                }
            }

            return ValidationResult.ValidResult;
        }
    }
}
