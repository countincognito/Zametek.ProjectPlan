using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class SlackColorFormatLookup
    {
        #region Fields

        private readonly IList<ActivitySeverityModel> m_ActivitySeverities;

        #endregion

        #region Ctors

        public SlackColorFormatLookup(IEnumerable<ActivitySeverityModel> activitySeverities)
        {
            if (activitySeverities == null)
            {
                throw new ArgumentNullException(nameof(activitySeverities));
            }
            m_ActivitySeverities = activitySeverities.OrderBy(x => x.SlackLimit).ToList();
        }

        #endregion

        #region Private Methods

        private T FindSlackColor<T>(
            int? totalSlack,
            Func<byte, byte, byte, byte, T> func)
        {
            if (!totalSlack.HasValue)
            {
                return func(255, 0, 0, 0);
            }
            int totalSlackValue = totalSlack.GetValueOrDefault();
            foreach (ActivitySeverityModel activitySeverity in m_ActivitySeverities)
            {
                if (totalSlackValue <= activitySeverity.SlackLimit)
                {
                    return func(
                        activitySeverity.ColorFormat.A,
                        activitySeverity.ColorFormat.R,
                        activitySeverity.ColorFormat.G,
                        activitySeverity.ColorFormat.B);
                }
            }
            return func(255, 0, 0, 0);
        }

        #endregion

        #region Public Methods

        public Color FindSlackColor(int? totalSlack)
        {
            return FindSlackColor(totalSlack, (a, r, g, b) => new Color { A = a, R = r, G = g, B = b });
        }

        public string FindSlackColorHexCode(int? totalSlack)
        {
            return FindSlackColor(totalSlack, Converter.HexConverter);
        }

        #endregion
    }
}
