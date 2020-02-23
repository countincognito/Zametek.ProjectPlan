using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class GraphXEdgeFormatLookup
    {
        #region Fields

        private static readonly IDictionary<EdgeDashStyle, GraphX.Controls.EdgeDashStyle> s_EdgeDashLookup;
        private static readonly IDictionary<EdgeWeightStyle, double> s_EdgeWeightLookup;
        private static double s_NormalStrokeWeight = 2.0;
        private static double s_BoldStrokeWeight = 5.0;

        private readonly IDictionary<EdgeType, GraphX.Controls.EdgeDashStyle> m_EdgeTypeDashLookup;
        private readonly IDictionary<EdgeType, double> m_EdgeTypeWeightLookup;

        #endregion

        #region Ctors

        static GraphXEdgeFormatLookup()
        {
            s_EdgeDashLookup = new Dictionary<EdgeDashStyle, GraphX.Controls.EdgeDashStyle>
            {
                {EdgeDashStyle.Normal, GraphX.Controls.EdgeDashStyle.Solid},
                {EdgeDashStyle.Dashed, GraphX.Controls.EdgeDashStyle.Dash}
            };
            s_EdgeWeightLookup = new Dictionary<EdgeWeightStyle, double>
            {
                {EdgeWeightStyle.Normal, s_NormalStrokeWeight},
                {EdgeWeightStyle.Bold, s_BoldStrokeWeight}
            };
        }

        public GraphXEdgeFormatLookup(IEnumerable<EdgeTypeFormatDto> edgeTypeFormatDtos)
        {
            if (edgeTypeFormatDtos == null)
            {
                throw new ArgumentNullException(nameof(edgeTypeFormatDtos));
            }
            m_EdgeTypeDashLookup = new Dictionary<EdgeType, GraphX.Controls.EdgeDashStyle>();
            m_EdgeTypeWeightLookup = new Dictionary<EdgeType, double>();
            foreach (EdgeTypeFormatDto edgeTypeFormatDto in edgeTypeFormatDtos)
            {
                m_EdgeTypeDashLookup.Add(edgeTypeFormatDto.EdgeType, s_EdgeDashLookup[edgeTypeFormatDto.EdgeDashStyle]);
                m_EdgeTypeWeightLookup.Add(edgeTypeFormatDto.EdgeType, s_EdgeWeightLookup[edgeTypeFormatDto.EdgeWeightStyle]);
            }
        }

        #endregion

        #region Public Methods

        public GraphX.Controls.EdgeDashStyle FindGraphXEdgeDashStyle(bool isCritical, bool isDummy)
        {
            if (isCritical)
            {
                if (isDummy)
                {
                    return m_EdgeTypeDashLookup[EdgeType.CriticalDummy];
                }
                else
                {
                    return m_EdgeTypeDashLookup[EdgeType.CriticalActivity];
                }
            }
            else
            {
                if (isDummy)
                {
                    return m_EdgeTypeDashLookup[EdgeType.Dummy];
                }
                else
                {
                    return m_EdgeTypeDashLookup[EdgeType.Activity];
                }
            }
        }

        public double FindStrokeThickness(bool isCritical, bool isDummy)
        {
            if (isCritical)
            {
                if (isDummy)
                {
                    return m_EdgeTypeWeightLookup[EdgeType.CriticalDummy];
                }
                else
                {
                    return m_EdgeTypeWeightLookup[EdgeType.CriticalActivity];
                }
            }
            else
            {
                if (isDummy)
                {
                    return m_EdgeTypeWeightLookup[EdgeType.Dummy];
                }
                else
                {
                    return m_EdgeTypeWeightLookup[EdgeType.Activity];
                }
            }
        }

        #endregion
    }
}
