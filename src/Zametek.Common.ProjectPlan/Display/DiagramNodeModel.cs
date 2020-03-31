using System;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class DiagramNodeModel
    {
        public int Id { get; set; }

        public double X { get; set; }

        public double Y { get; set; }

        public double Height { get; set; }

        public double Width { get; set; }

        public string FillColorHexCode { get; set; }

        public string BorderColorHexCode { get; set; }

        public string Text { get; set; }
    }
}
