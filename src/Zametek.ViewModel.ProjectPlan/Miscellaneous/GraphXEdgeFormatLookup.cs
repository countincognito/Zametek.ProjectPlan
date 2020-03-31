using System;
using System.Collections.Generic;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GraphXEdgeFormatLookup
    {
        #region Fields

        private static readonly IDictionary<EdgeDashStyle, GraphX.Controls.EdgeDashStyle> s_EdgeDashLookup =
            new Dictionary<EdgeDashStyle, GraphX.Controls.EdgeDashStyle>
            {
                {EdgeDashStyle.Normal, GraphX.Controls.EdgeDashStyle.Solid},
                {EdgeDashStyle.Dashed, GraphX.Controls.EdgeDashStyle.Dash}
            };
        private static readonly IDictionary<EdgeWeightStyle, double> s_EdgeWeightLookup =
            new Dictionary<EdgeWeightStyle, double>
            {
                {EdgeWeightStyle.Normal, s_NormalStrokeWeight},
                {EdgeWeightStyle.Bold, s_BoldStrokeWeight}
            };
        private static double s_NormalStrokeWeight = 2.0;
        private static double s_BoldStrokeWeight = 5.0;

        private readonly IDictionary<EdgeType, GraphX.Controls.EdgeDashStyle> m_EdgeTypeDashLookup;
        private readonly IDictionary<EdgeType, double> m_EdgeTypeWeightLookup;

        #endregion

        #region Ctors

        public GraphXEdgeFormatLookup(IEnumerable<EdgeTypeFormatModel> edgeTypeFormats)
        {
            if (edgeTypeFormats == null)
            {
                throw new ArgumentNullException(nameof(edgeTypeFormats));
            }
            m_EdgeTypeDashLookup = new Dictionary<EdgeType, GraphX.Controls.EdgeDashStyle>();
            m_EdgeTypeWeightLookup = new Dictionary<EdgeType, double>();
            foreach (EdgeTypeFormatModel edgeTypeFormat in edgeTypeFormats)
            {
                m_EdgeTypeDashLookup.Add(edgeTypeFormat.EdgeType, s_EdgeDashLookup[edgeTypeFormat.EdgeDashStyle]);
                m_EdgeTypeWeightLookup.Add(edgeTypeFormat.EdgeType, s_EdgeWeightLookup[edgeTypeFormat.EdgeWeightStyle]);
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
