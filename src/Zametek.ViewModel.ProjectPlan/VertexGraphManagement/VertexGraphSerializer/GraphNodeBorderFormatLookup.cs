using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class GraphNodeBorderFormatLookup
    {
        #region Fields

        private static readonly Dictionary<NodeBorderWeightStyle, double> s_NodeBorderWeightLookup =
            new()
            {
                {NodeBorderWeightStyle.Normal, c_NormalStrokeWeight},
                {NodeBorderWeightStyle.Bold, c_BoldStrokeWeight}
            };
        private const double c_NormalStrokeWeight = 1.0;
        private const double c_BoldStrokeWeight = 2.0;

        private readonly Dictionary<NodeType, NodeBorderDashStyle> m_NodeTypeDashLookup;
        private readonly Dictionary<NodeType, double> m_NodeTypeWeightLookup;

        #endregion

        #region Ctors

        public GraphNodeBorderFormatLookup(IEnumerable<NodeTypeFormatModel> nodeTypeFormats)
        {
            ArgumentNullException.ThrowIfNull(nodeTypeFormats);
            m_NodeTypeDashLookup = [];
            m_NodeTypeWeightLookup = [];

            foreach (NodeTypeFormatModel nodeTypeFormat in nodeTypeFormats)
            {
                m_NodeTypeDashLookup.Add(nodeTypeFormat.NodeType, nodeTypeFormat.NodeBorderDashStyle);
                m_NodeTypeWeightLookup.Add(nodeTypeFormat.NodeType, s_NodeBorderWeightLookup[nodeTypeFormat.NodeBorderWeightStyle]);
            }
        }

        #endregion

        #region Public Methods

        public NodeBorderDashStyle FindGraphNodeBorderDashStyle(bool isCritical, bool isDummy)
        {
            if (isCritical)
            {
                if (isDummy)
                {
                    return m_NodeTypeDashLookup[NodeType.CriticalDummy];
                }
                else
                {
                    return m_NodeTypeDashLookup[NodeType.CriticalActivity];
                }
            }
            else
            {
                if (isDummy)
                {
                    return m_NodeTypeDashLookup[NodeType.Dummy];
                }
                else
                {
                    return m_NodeTypeDashLookup[NodeType.Activity];
                }
            }
        }

        public double FindBorderThickness(bool isCritical, bool isDummy)
        {
            if (isCritical)
            {
                if (isDummy)
                {
                    return m_NodeTypeWeightLookup[NodeType.CriticalDummy];
                }
                else
                {
                    return m_NodeTypeWeightLookup[NodeType.CriticalActivity];
                }
            }
            else
            {
                if (isDummy)
                {
                    return m_NodeTypeWeightLookup[NodeType.Dummy];
                }
                else
                {
                    return m_NodeTypeWeightLookup[NodeType.Activity];
                }
            }
        }

        #endregion
    }
}
