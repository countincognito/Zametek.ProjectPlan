using System;
using System.IO;
using System.Xml.Serialization;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    public class GraphProcessingEngine
        : IGraphProcessingEngine
    {
        #region IGraphProcessingEngine Members

        public byte[] ExportArrowGraphToDiagram(DiagramArrowGraphDto diagramArrowGraphDto)
        {
            if (diagramArrowGraphDto == null)
            {
                throw new ArgumentNullException(nameof(diagramArrowGraphDto));
            }
            graphml graphML = GraphMLBuilder.ToGraphML(diagramArrowGraphDto);
            byte[] output = null;
            using (var ms = new MemoryStream())
            {
                var xmlSerializer = new XmlSerializer(typeof(graphml));
                xmlSerializer.Serialize(ms, graphML);
                output = ms.ToArray();
            }
            return output;
        }

        #endregion
    }
}
