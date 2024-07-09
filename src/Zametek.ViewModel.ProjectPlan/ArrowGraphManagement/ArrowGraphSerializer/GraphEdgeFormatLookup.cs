using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GraphEdgeFormatLookup
    {
        #region Fields

        private static readonly IDictionary<EdgeWeightStyle, int> s_EdgeWeightLookup =
            new Dictionary<EdgeWeightStyle, int>
            {
                {EdgeWeightStyle.Normal, c_NormalStrokeWeight},
                {EdgeWeightStyle.Bold, c_BoldStrokeWeight}
            };
        private const int c_NormalStrokeWeight = 2;
        private const int c_BoldStrokeWeight = 5;

        private readonly Dictionary<EdgeType, EdgeDashStyle> m_EdgeTypeDashLookup;
        private readonly Dictionary<EdgeType, int> m_EdgeTypeWeightLookup;

        #endregion

        #region Ctors

        public GraphEdgeFormatLookup(IEnumerable<EdgeTypeFormatModel> edgeTypeFormats)
        {
            ArgumentNullException.ThrowIfNull(edgeTypeFormats);
            m_EdgeTypeDashLookup = [];
            m_EdgeTypeWeightLookup = [];

            foreach (EdgeTypeFormatModel edgeTypeFormat in edgeTypeFormats)
            {
                m_EdgeTypeDashLookup.Add(edgeTypeFormat.EdgeType, edgeTypeFormat.EdgeDashStyle);
                m_EdgeTypeWeightLookup.Add(edgeTypeFormat.EdgeType, s_EdgeWeightLookup[edgeTypeFormat.EdgeWeightStyle]);
            }
        }

        #endregion

        #region Public Methods

        public EdgeDashStyle FindGraphEdgeDashStyle(bool isCritical, bool isDummy)
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

        public int FindStrokeThickness(bool isCritical, bool isDummy)
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
