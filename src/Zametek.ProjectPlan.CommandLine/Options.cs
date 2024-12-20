using CommandLine;
using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class Options
    {
        [Option('i', "input", Group = "file-in", HelpText = "Input file path")]
        public string? InputFilename { get; set; } = default;

        [Option('m', "import", Group = "file-in", HelpText = "Import file path - must end in (.mpp|.xlsx)")]
        public string? ImportFilename { get; set; } = default;



        [Option('o', "output", HelpText = "Output file path")]
        public string? OutputFilename { get; set; } = default;

        [Option('x', "export", HelpText = "Export file path - must end in .xlsx")]
        public string? ExportFilename { get; set; } = default;



        [Option('b', "business-days", Default = true, Required = false, HelpText = "Use business days (true|false)")]
        public bool? UseBusinessDays { get; set; } = true;

        [Option('f', "classic-dates", Default = false, Required = false, HelpText = "Classic date format (true|false)")]
        public bool? ClassicDateFormat { get; set; } = false;

        [Option('d', "show-dates", Default = false, Required = false, HelpText = "Show dates (true|false)")]
        public bool? ShowDates { get; set; } = false;

        [Option('t', "base-theme", Default = BaseTheme.Light, Required = false, HelpText = "Output theme (Light|Dark)")]
        public BaseTheme BaseTheme { get; set; } = BaseTheme.Light;



        [Option("gantt-directory", HelpText = "Gantt chart output file directory")]
        public string? GanttDirectory { get; set; } = default;

        [Option("gantt-format", Default = PlotExport.Jpeg, HelpText = "Gantt chart format (Jpeg|Png|Pdf)")]
        public PlotExport GanttFormat { get; set; } = default;

        [Option("gantt-group", Default = GroupByMode.None, HelpText = "Gantt chart group (None|Resource|WorkStream)")]
        public GroupByMode GanttGroup { get; set; } = default;

        [Option("gantt-annotate", Default = AnnotationStyle.None, HelpText = "Annotate gantt chart group (None|Plain|Color)")]
        public AnnotationStyle GanttAnnotate { get; set; } = default;

        [Option("gantt-label", Default = false, Required = false, HelpText = "Label gantt chart annotations (true|false)")]
        public bool? GanttLabel { get; set; } = false;

        [Option("gantt-finish", Default = false, Required = false, HelpText = "Show project finish in gantt chart (true|false)")]
        public bool? GanttFinish { get; set; } = false;

        [Option("gantt-tracking", Default = false, Required = false, HelpText = "Show tracking in gantt chart (true|false)")]
        public bool? GanttTracking { get; set; } = false;



        [Option("gantt-size", Min = 2, Max = 2, Separator = ':', HelpText = "Gantt chart dimensions in pixels (<width>:<height>)")]
        public IEnumerable<int> GanttSize { get; set; } = [];



        [Option("graph-directory", HelpText = "Arrow graph output file directory")]
        public string? GraphDirectory { get; set; } = default;

        [Option("graph-format", Default = GraphExport.Jpeg, HelpText = "Arrow graph format (Jpeg|Png|Pdf|Svg|GraphML|Dot)")]
        public GraphExport GraphFormat { get; set; } = default;



        [Option("resource-directory", HelpText = "Resource chart output file directory")]
        public string? ResourceDirectory { get; set; } = default;

        [Option("resource-format", Default = PlotExport.Jpeg, HelpText = "Resource chart format (Jpeg|Png|Pdf)")]
        public PlotExport ResourceFormat { get; set; } = default;

        [Option("resource-size", Min = 2, Max = 2, Separator = ':', HelpText = "Resource chart dimensions in pixels (<width>:<height>)")]
        public IEnumerable<int> ResourceSize { get; set; } = [];



        [Option("ev-directory", HelpText = "Earned-Value chart output file directory")]
        public string? EVDirectory { get; set; } = default;

        [Option("ev-format", Default = PlotExport.Jpeg, HelpText = "Earned-Value chart format (Jpeg|Png|Pdf)")]
        public PlotExport EVFormat { get; set; } = default;

        [Option("ev-projections", Default = false, HelpText = "Earned-Value chart group (true|false)")]
        public bool? EVProjections { get; set; } = default;

        [Option("ev-size", Min = 2, Max = 2, Separator = ':', HelpText = "Earned-Value chart dimensions in pixels (<width>:<height>)")]
        public IEnumerable<int> EVSize { get; set; } = [];
    }
}
