using GraphX.PCL.Common.Models;
using System;
using Zametek.Common.Project;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    [Serializable]
    public class ArrowGraphVertex
        : VertexBase
    {
        #region Fields

        private EventDto m_EventVertex;

        #endregion

        #region Ctors

        public ArrowGraphVertex()
        { }

        public ArrowGraphVertex(
            EventDto eventVertexDto,
            NodeType nodeType)
            : this()
        {
            m_EventVertex = eventVertexDto ?? throw new ArgumentNullException(nameof(eventVertexDto));
            ID = m_EventVertex.Id;
            NodeType = nodeType;
        }

        #endregion

        #region Properties

        public int? EarliestFinishTime => m_EventVertex.EarliestFinishTime;

        public int? LatestFinishTime => m_EventVertex.LatestFinishTime;

        public NodeType NodeType
        {
            get;
            private set;
        }

        #endregion

        #region Overrides

        public override string ToString()
        {
            int? eft = EarliestFinishTime;
            int? lft = LatestFinishTime;
            if (eft.HasValue
                && lft.HasValue)
            {
                return $@"{eft.Value}|{lft.Value}";
            }
            return string.Empty;
        }

        #endregion
    }
}
