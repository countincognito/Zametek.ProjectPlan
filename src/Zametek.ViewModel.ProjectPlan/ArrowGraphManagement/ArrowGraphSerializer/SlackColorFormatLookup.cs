using Avalonia.Media;
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
            ArgumentNullException.ThrowIfNull(activitySeverities);
            m_ActivitySeverities = [.. activitySeverities.OrderBy(x => x.SlackLimit)];
        }

        #endregion

        #region Private Methods

        private T FindSlackColor<T>(
            int? totalSlack,
            Func<byte, byte, byte, byte, T> func)
        {
            ArgumentNullException.ThrowIfNull(func);
            if (!totalSlack.HasValue)
            {
                return func(byte.MaxValue, 0, 0, 0);
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
            return func(byte.MaxValue, 0, 0, 0);
        }

        #endregion

        #region Public Methods

        public Color FindSlackColor(int? totalSlack)
        {
            return FindSlackColor(totalSlack, (a, r, g, b) => new Color(a, r, g, b));
        }

        public ColorFormatModel FindSlackColorFormat(int? totalSlack)
        {
            return FindSlackColor(totalSlack, (a, r, g, b) => new ColorFormatModel
            {
                A = a,
                R = r,
                G = g,
                B = b
            });
        }

        #endregion
    }
}
