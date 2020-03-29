using GraphX.PCL.Common.Models;
using System;

namespace Zametek.Common.ProjectPlan
{
    public class ArrowGraphEdge
         : EdgeBase<ArrowGraphVertex>
    {
        #region Fields

        private ActivityModel m_Activity;

        #endregion

        #region Ctors

        public ArrowGraphEdge(
            ActivityModel activity,
            ArrowGraphVertex source,
            ArrowGraphVertex target)
            : base(source, target, 1.0)
        {
            m_Activity = activity ?? throw new ArgumentNullException(nameof(activity));
            ID = m_Activity.Id;
        }

        public ArrowGraphEdge()
            : base(null, null)
        {
        }

        #endregion

        #region Properties

        public int ActivityId => m_Activity.Id;

        public string Name => m_Activity.Name;

        public bool IsDummy
        {
            get
            {
                int? duration = Duration;
                if (duration.HasValue)
                {
                    return duration.Value == 0;
                }
                return false;
            }
        }

        public int? Duration => m_Activity.Duration;

        public int? TotalSlack
        {
            get
            {
                int? latestFinishTime = LatestFinishTime;
                int? earliestFinishTime = EarliestFinishTime;
                if (latestFinishTime.HasValue
                    && earliestFinishTime.HasValue)
                {
                    return latestFinishTime.Value - earliestFinishTime.Value;
                }
                return null;
            }
        }

        public int? FreeSlack => m_Activity.FreeSlack;

        public int? MinimumFreeSlack => m_Activity.MinimumFreeSlack;

        public int? InterferingSlack
        {
            get
            {
                int? totalSlack = TotalSlack;
                int? freeSlack = FreeSlack;
                if (totalSlack.HasValue
                    && freeSlack.HasValue)
                {
                    return totalSlack.Value - freeSlack.Value;
                }
                return null;
            }
        }

        public bool IsCritical
        {
            get
            {
                int? totalSlack = TotalSlack;
                if (totalSlack.HasValue)
                {
                    return totalSlack.Value == 0;
                }
                return false;
            }
        }

        public bool IsNotCritical => !IsCritical;

        public int? EarliestStartTime => m_Activity.EarliestStartTime;

        public int? LatestStartTime
        {
            get
            {
                int? latestFinishTime = LatestFinishTime;
                int? duration = Duration;
                if (latestFinishTime.HasValue
                    && duration.HasValue)
                {
                    return latestFinishTime.Value - duration.Value;
                }
                return null;
            }
        }

        public int? EarliestFinishTime
        {
            get
            {
                int? earliestStartTime = EarliestStartTime;
                int? duration = Duration;
                if (earliestStartTime.HasValue
                    && duration.HasValue)
                {
                    return earliestStartTime.Value + duration.Value;
                }
                return null;
            }
        }

        public int? LatestFinishTime => m_Activity.LatestFinishTime;

        public bool CanBeRemoved => m_Activity.CanBeRemoved;

        public bool CannotBeRemoved => !CanBeRemoved;

        public string ForegroundHexCode
        {
            get;
            set;
        }

        public double StrokeThickness
        {
            get;
            set;
        }

        public GraphX.Controls.EdgeDashStyle DashStyle
        {
            get;
            set;
        }

        #endregion
    }
}
