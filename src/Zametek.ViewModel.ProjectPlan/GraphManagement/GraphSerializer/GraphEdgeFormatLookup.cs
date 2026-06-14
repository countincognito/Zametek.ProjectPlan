using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GraphEdgeFormatLookup
    {
        #region Fields

        private static readonly Dictionary<EdgeWeightStyle, double> s_EdgeWeightLookup =
            new()
            {
                {EdgeWeightStyle.Normal, c_NormalStrokeWeight},
                {EdgeWeightStyle.Bold, c_BoldStrokeWeight}
            };
        private const double c_NormalStrokeWeight = 1.0;
        private const double c_BoldStrokeWeight = 2.0;

        private readonly Dictionary<EdgeType, EdgeDashStyle> m_EdgeTypeDashLookup;
        private readonly Dictionary<EdgeType, double> m_EdgeTypeWeightLookup;

        #endregion

        #region Ctors

        public GraphEdgeFormatLookup(IEnumerable<EdgeTypeFormatModel> edgeTypeFormats)
        {
            ArgumentNullException.ThrowIfNull(edgeTypeFormats);
            m_EdgeTypeDashLookup = [];
            m_EdgeTypeWeightLookup = [];

            // Check the input collection contains exactly one entry for each EdgeType.
            HashSet<EdgeType> uniqueEdgeTypes = [.. edgeTypeFormats.Select(x => x.EdgeType)];
            HashSet<EdgeType> allEdgeTypes = [.. Enum.GetValues<EdgeType>()];

            if (uniqueEdgeTypes.Count != allEdgeTypes.Count
                || !uniqueEdgeTypes.SetEquals(allEdgeTypes))
            {
                throw new ArgumentException("The edge type formats collection must contain exactly one entry for each EdgeType.", nameof(edgeTypeFormats));
            }

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
