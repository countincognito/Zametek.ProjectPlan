using Zametek.Graphs.Avalonia;

namespace Zametek.Graphs.Avalonia.TestApp.Graphs
{
    // Hand-built sample graphs, expressed directly as the library's neutral DiagramGraphModel (nodes and
    // edges with resolved presentation and finished label text - no coordinates). This is exactly the
    // input a real consumer produces from its own domain model and hands to the interactive view-model;
    // building it in code keeps the demo self-contained (no project files, no compile pipeline) and shows
    // the shape of the contract. MSAGL computes the positions from this at layout time.
    //
    // Two classic project-network shapes are provided:
    //   * Arrow  - activity-on-arrow: events are nodes, activities are the labelled edges.
    //   * Vertex - activity-on-node: activities are nodes (three-line boxes), dependencies are edges.
    // The same data drives both the default-styled and the bespoke-styled tabs, so the difference you see
    // between them is purely presentation (templates + GraphAppearance), not the underlying graph.
    internal static class SampleGraphs
    {
        // A muted palette that reads on both the light and dark canvas backgrounds.
        private const string CriticalStroke = @"#C0392B"; // red - the critical path
        private const string NormalEdgeStroke = @"#667085"; // slate grey
        private const string EventFill = @"#EAF1FB";
        private const string EventBorder = @"#33475B";
        private const string CriticalNodeFill = @"#F7D7D5";
        private const string CriticalNodeBorder = @"#C0392B";
        private const string NormalNodeFill = @"#E3EFE3";
        private const string NormalNodeBorder = @"#2E7D32";

        // Activity-on-arrow. Event nodes carry a single-line number; activity edges carry a label
        // (optionally the activity name, honouring the "show names" toggle) plus a hover tooltip. The
        // critical path (A-C-F-H) is drawn heavier and red, and there is one dashed dummy activity.
        public static DiagramGraphModel BuildArrow(bool showNames)
        {
            var nodes = new List<DiagramNodeModel>();
            for (int eventId = 1; eventId <= 7; eventId++)
            {
                nodes.Add(new DiagramNodeModel
                {
                    Id = eventId,
                    Text = eventId.ToString(),
                    FillColorHexCode = EventFill,
                    BorderColorHexCode = EventBorder,
                    BorderThickness = 1.2,
                    BorderDashStyle = GraphDashStyle.Normal,
                    Tooltip = $@"Event {eventId}",
                });
            }

            var edges = new List<DiagramEdgeModel>
            {
                Activity(1, @"A", 1, 2, duration: 3, critical: true, showNames),
                Activity(2, @"B", 1, 3, duration: 2, critical: false, showNames),
                Activity(3, @"C", 2, 4, duration: 4, critical: true, showNames),
                Activity(4, @"D", 3, 4, duration: 1, critical: false, showNames),
                Activity(5, @"E", 3, 5, duration: 3, critical: false, showNames),
                Dummy(6, sourceId: 4, targetId: 5),
                Activity(7, @"F", 4, 6, duration: 5, critical: true, showNames),
                Activity(8, @"G", 5, 6, duration: 2, critical: false, showNames),
                Activity(9, @"H", 6, 7, duration: 2, critical: true, showNames),
            };

            return new DiagramGraphModel { Nodes = nodes, Edges = edges };
        }

        // Activity-on-node. Each node is an activity drawn as a three-line box (earliest start/finish,
        // name, latest start/finish); dependency edges carry no label (the vertex graph never shows them).
        // The critical activities (A-C-F-H) and the edges between them are red.
        public static DiagramGraphModel BuildVertex()
        {
            var nodes = new List<DiagramNodeModel>
            {
                ActivityNode(1, @"A", es: 0, ef: 3, ls: 0, lf: 3, critical: true),
                ActivityNode(2, @"B", es: 0, ef: 2, ls: 1, lf: 3, critical: false),
                ActivityNode(3, @"C", es: 3, ef: 7, ls: 3, lf: 7, critical: true),
                ActivityNode(4, @"D", es: 2, ef: 3, ls: 3, lf: 4, critical: false),
                ActivityNode(5, @"E", es: 3, ef: 6, ls: 5, lf: 8, critical: false),
                ActivityNode(6, @"F", es: 7, ef: 12, ls: 7, lf: 12, critical: true),
                ActivityNode(7, @"G", es: 6, ef: 8, ls: 8, lf: 10, critical: false),
                ActivityNode(8, @"H", es: 12, ef: 14, ls: 12, lf: 14, critical: true),
            };

            var edges = new List<DiagramEdgeModel>
            {
                Dependency(1, 1, 3, critical: true),  // A -> C
                Dependency(2, 1, 4, critical: false), // A -> D
                Dependency(3, 2, 4, critical: false), // B -> D
                Dependency(4, 3, 6, critical: true),  // C -> F
                Dependency(5, 4, 5, critical: false), // D -> E
                Dependency(6, 4, 6, critical: false), // D -> F
                Dependency(7, 5, 7, critical: false), // E -> G
                Dependency(8, 6, 8, critical: true),  // F -> H
                Dependency(9, 7, 8, critical: false), // G -> H
            };

            return new DiagramGraphModel { Nodes = nodes, Edges = edges };
        }

        private static DiagramEdgeModel Activity(int id, string name, int sourceId, int targetId, int duration, bool critical, bool showNames)
        {
            return new DiagramEdgeModel
            {
                Id = id,
                Name = name,
                SourceId = sourceId,
                TargetId = targetId,
                Label = showNames ? $@"{name}·{duration}" : duration.ToString(),
                ShowLabel = true,
                ForegroundColorHexCode = critical ? CriticalStroke : NormalEdgeStroke,
                StrokeThickness = critical ? 2.2 : 1.2,
                DashStyle = GraphDashStyle.Normal,
                Tooltip = $@"Activity {name} (duration {duration})",
            };
        }

        private static DiagramEdgeModel Dummy(int id, int sourceId, int targetId)
        {
            return new DiagramEdgeModel
            {
                Id = id,
                SourceId = sourceId,
                TargetId = targetId,
                Label = string.Empty,
                ShowLabel = false,
                ForegroundColorHexCode = NormalEdgeStroke,
                StrokeThickness = 1.0,
                DashStyle = GraphDashStyle.Dashed,
                Tooltip = @"Dummy activity",
            };
        }

        private static DiagramNodeModel ActivityNode(int id, string name, int es, int ef, int ls, int lf, bool critical)
        {
            // Three monospace lines, centred by the renderer: earliest times, the activity name, latest times.
            string label = $"{es,2} {ef,2}\n  {name}\n{ls,2} {lf,2}";
            return new DiagramNodeModel
            {
                Id = id,
                Text = label,
                FillColorHexCode = critical ? CriticalNodeFill : NormalNodeFill,
                BorderColorHexCode = critical ? CriticalNodeBorder : NormalNodeBorder,
                BorderThickness = 1.2,
                BorderDashStyle = GraphDashStyle.Normal,
                Tooltip = $@"Activity {name}  ES={es} EF={ef}  LS={ls} LF={lf}",
            };
        }

        private static DiagramEdgeModel Dependency(int id, int sourceId, int targetId, bool critical)
        {
            return new DiagramEdgeModel
            {
                Id = id,
                SourceId = sourceId,
                TargetId = targetId,
                Label = string.Empty,
                ShowLabel = false,
                ForegroundColorHexCode = critical ? CriticalStroke : NormalEdgeStroke,
                StrokeThickness = critical ? 2.0 : 1.3,
                DashStyle = GraphDashStyle.Normal,
            };
        }
    }
}
