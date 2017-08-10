using System;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ProjectStartUpdatedPayload
    {
        #region Ctors

        public ProjectStartUpdatedPayload(DateTime projectStart)
        {
            ProjectStart = projectStart;
        }

        #endregion

        #region Properties

        public DateTime ProjectStart
        {
            get;
        }

        #endregion
    }
}
