using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ActivitySeverityLookup
    {
        #region Fields

        private readonly IList<ActivitySeverityModel> m_ActivitySeverities;

        #endregion

        #region Ctors

        public ActivitySeverityLookup(IEnumerable<ActivitySeverityModel> activitySeverities)
        {
            if (activitySeverities == null)
            {
                throw new ArgumentNullException(nameof(activitySeverities));
            }
            m_ActivitySeverities = activitySeverities.OrderBy(x => x.SlackLimit).ToList();
        }

        #endregion

        #region Public Methods

        public double FindSlackCriticalityWeight(int? totalSlack)
        {
            if (!totalSlack.HasValue)
            {
                return 1.0;
            }
            int totalSlackValue = totalSlack.GetValueOrDefault();
            foreach (ActivitySeverityModel activitySeverity in m_ActivitySeverities)
            {
                if (totalSlackValue <= activitySeverity.SlackLimit)
                {
                    return activitySeverity.CriticalityWeight;
                }
            }
            return 1.0;
        }

        public double CriticalCriticalityWeight()
        {
            return m_ActivitySeverities.Aggregate((i1, i2) => i1.SlackLimit < i2.SlackLimit ? i1 : i2).CriticalityWeight;
        }

        public double FindSlackFibonacciWeight(int? totalSlack)
        {
            if (!totalSlack.HasValue)
            {
                return 1.0;
            }
            int totalSlackValue = totalSlack.GetValueOrDefault();
            foreach (ActivitySeverityModel activitySeverity in m_ActivitySeverities)
            {
                if (totalSlackValue <= activitySeverity.SlackLimit)
                {
                    return activitySeverity.FibonacciWeight;
                }
            }
            return 1.0;
        }

        public double CriticalFibonacciWeight()
        {
            return m_ActivitySeverities.Aggregate((i1, i2) => i1.SlackLimit < i2.SlackLimit ? i1 : i2).FibonacciWeight;
        }

        #endregion
    }
}
