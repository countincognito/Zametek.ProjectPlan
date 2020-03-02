using System;

namespace Zametek.Common.ProjectPlan
{
    [Serializable]
    public class DiagramEdgeDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int SourceId { get; set; }
        public int TargetId { get; set; }
        public Project.v0_1_0.EdgeDashStyle DashStyle { get; set; }
        public string ForegroundColorHexCode { get; set; }
        public double StrokeThickness { get; set; }
        public string Label { get; set; }
        public bool ShowLabel { get; set; }
    }
}
