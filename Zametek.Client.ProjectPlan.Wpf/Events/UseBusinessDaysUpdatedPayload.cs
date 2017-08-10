namespace Zametek.Client.ProjectPlan.Wpf
{
    public class UseBusinessDaysUpdatedPayload
    {
        #region Ctors

        public UseBusinessDaysUpdatedPayload(bool useBusinessDays)
        {
            UseBusinessDays = useBusinessDays;
        }

        #endregion

        #region Properties

        public bool UseBusinessDays
        {
            get;
        }

        #endregion
    }
}
