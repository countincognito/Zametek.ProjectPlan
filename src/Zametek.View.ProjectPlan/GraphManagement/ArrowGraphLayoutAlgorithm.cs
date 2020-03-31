using GraphX.Measure;
using GraphX.PCL.Common.Interfaces;
using GraphX.PCL.Logic.Algorithms.LayoutAlgorithms;
using QuickGraph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Zametek.View.ProjectPlan
{
    public class ArrowGraphLayoutAlgorithm<TVertex, TEdge, TGraph>
        : IExternalLayout<TVertex, TEdge>
        where TVertex : class
        where TEdge : IEdge<TVertex>
        where TGraph : IVertexAndEdgeListGraph<TVertex, TEdge>, IMutableVertexAndEdgeSet<TVertex, TEdge>
    {
        #region Fields

        private readonly TGraph m_Graph;
        private readonly double m_OffsetX;
        private double m_OffsetY;
        private readonly double m_RateX;
        private readonly double m_RateY;

        #endregion

        #region Ctors

        public ArrowGraphLayoutAlgorithm(
            TGraph graph,
            double rateX = 1.0,
            double rateY = 2.5,
            double offsetX = 0,
            double offsetY = 600)
        {
            if (graph == null)
            {
                throw new ArgumentNullException(nameof(graph));
            }
            if (rateX <= 0)
            {
                throw new ArgumentException("RateX cannot be less than or equal to 0");
            }
            if (rateY <= 0)
            {
                throw new ArgumentException("RateY cannot be less than or equal to 0");
            }
            m_Graph = graph;
            m_RateX = rateX;
            m_RateY = rateY;
            m_OffsetX = offsetX;
            m_OffsetY = offsetY;
        }

        #endregion

        #region IExternalLayout<TVertex> Members

        public void Compute(CancellationToken cancellationToken)
        {
            var eslaParameters = new EfficientSugiyamaLayoutParameters
            {
                MinimizeEdgeLength = true,
                LayerDistance = 120
            };
            var esla = new EfficientSugiyamaLayoutAlgorithm<TVertex, TEdge, TGraph>(m_Graph, eslaParameters, VertexPositions, VertexSizes);
            esla.Compute(cancellationToken);
            VertexPositions = new Dictionary<TVertex, Point>();
            double offsetY = esla.VertexPositions.Values.Min(p => p.X);
            if (offsetY < 0)
            {
                m_OffsetY = -offsetY;
            }
            foreach (KeyValuePair<TVertex, Point> kvp in esla.VertexPositions)
            {
                VertexPositions.Add(
                    kvp.Key,
                    new Point(
                        kvp.Value.Y * m_RateX + m_OffsetX,
                        kvp.Value.X * m_RateY + m_OffsetY));
            }
        }

        public void ResetGraph(IEnumerable<TVertex> vertices, IEnumerable<TEdge> edges)
        {
            throw new NotImplementedException();
        }

        public bool NeedVertexSizes => true;

        public IDictionary<TVertex, Point> VertexPositions
        {
            get;
            private set;
        }


        [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Interface implementation")]
        public IDictionary<TVertex, Size> VertexSizes
        {
            get;
            set;
        }

        public bool SupportsObjectFreeze => true;

        #endregion
    }
}
