using GraphX.PCL.Common.Models;
using System;
using Zametek.Maths.Graphs;

namespace Zametek.Common.ProjectPlan
{
    public class ArrowGraphVertex
        : VertexBase
    {
        #region Fields

        private readonly EventModel m_EventVertex;

        #endregion

        #region Ctors

        public ArrowGraphVertex()
        {
        }

        public ArrowGraphVertex(
            EventModel eventVertex,
            NodeType nodeType)
            : this()
        {
            m_EventVertex = eventVertex ?? throw new ArgumentNullException(nameof(eventVertex));
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
