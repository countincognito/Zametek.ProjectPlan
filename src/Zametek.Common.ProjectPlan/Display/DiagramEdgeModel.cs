using System;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class DiagramEdgeModel
    {
        public int Id { get; set; }

        public string Name { get; set; }

        public int SourceId { get; set; }

        public int TargetId { get; set; }

        public EdgeDashStyle DashStyle { get; set; }

        public string ForegroundColorHexCode { get; set; }

        public double StrokeThickness { get; set; }

        public string Label { get; set; }

        public bool ShowLabel { get; set; }
    }
}
