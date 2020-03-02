using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class SlackColorFormatLookup
    {
        #region Fields

        private readonly IList<Common.Project.v0_1_0.ActivitySeverityDto> m_ActivitySeverityDtos;

        #endregion

        #region Ctors

        public SlackColorFormatLookup(IEnumerable<Common.Project.v0_1_0.ActivitySeverityDto> activitySeverityDtos)
        {
            if (activitySeverityDtos == null)
            {
                throw new ArgumentNullException(nameof(activitySeverityDtos));
            }
            m_ActivitySeverityDtos = activitySeverityDtos.OrderBy(x => x.SlackLimit).ToList();
        }

        #endregion

        #region Private Methods

        private T FindSlackColor<T>(int? totalSlack, Func<byte, byte, byte, byte, T> func)
        {
            if (!totalSlack.HasValue)
            {
                return func(255, 0, 0, 0);
            }
            int totalSlackValue = totalSlack.GetValueOrDefault();
            foreach (Common.Project.v0_1_0.ActivitySeverityDto activitySeverityDto in m_ActivitySeverityDtos)
            {
                if (totalSlackValue <= activitySeverityDto.SlackLimit)
                {
                    return func(
                        activitySeverityDto.ColorFormat.A,
                        activitySeverityDto.ColorFormat.R,
                        activitySeverityDto.ColorFormat.G,
                        activitySeverityDto.ColorFormat.B);
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
            return FindSlackColor(totalSlack, Common.Project.v0_1_0.DtoConverter.HexConverter);
        }

        #endregion
    }
}
