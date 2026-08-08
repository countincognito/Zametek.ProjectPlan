using CommandLine;
using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class Options
    {
        // Each option's long name is declared once here and referenced from the
        // Option attributes below, so a rename flows everywhere automatically:
        // through the parser, through Program.OptionLongName (which reads the
        // names back off the attributes for error messages), and through the
        // HelpText strings that mention other options (attribute arguments must
        // be compile-time constants, so those cross-references concatenate these
        // consts rather than calling the helper).
        private const string c_InputLongName = "input";
        private const string c_ImportLongName = "import";
        private const string c_ScenarioLongName = "scenario";
        private const string c_ListScenariosLongName = "list-scenarios";
        private const string c_OutputLongName = "output";
        private const string c_ExportLongName = "export";
        private const string c_BaseThemeLongName = "base-theme";
        private const string c_VerboseLongName = "verbose";
        private const string c_MetricsFormatLongName = "metrics-format";
        private const string c_GanttDirectoryLongName = "gantt-directory";
        private const string c_GanttFormatLongName = "gantt-format";
        private const string c_GanttSizeLongName = "gantt-size";
        private const string c_ArrowGraphDirectoryLongName = "arrow-directory";
        private const string c_ArrowGraphFormatLongName = "arrow-format";
        private const string c_VertexGraphDirectoryLongName = "vertex-directory";
        private const string c_VertexGraphFormatLongName = "vertex-format";
        private const string c_ResourceDirectoryLongName = "resource-directory";
        private const string c_ResourceFormatLongName = "resource-format";
        private const string c_ResourceSizeLongName = "resource-size";
        private const string c_EVDirectoryLongName = "ev-directory";
        private const string c_EVFormatLongName = "ev-format";
        private const string c_EVSizeLongName = "ev-size";
        private const string c_ScenarioChartDirectoryLongName = "scenario-chart-directory";
        private const string c_ScenarioChartFormatLongName = "scenario-chart-format";
        private const string c_ScenarioChartSizeLongName = "scenario-chart-size";

        [Option('i', c_InputLongName, Group = "file-in", HelpText = "Input file path")]
        public string? InputFilename { get; set; } = default;

        [Option('m', c_ImportLongName, Group = "file-in", HelpText = "Import file path - must end in (.mpp|.xlsx)")]
        public string? ImportFilename { get; set; } = default;



        [Option('s', c_ScenarioLongName, HelpText = "Scenario to load, by name or id - the full id or a unique prefix of at least 4 hex characters (defaults to the project's current scenario; only valid with --" + c_InputLongName + ")")]
        public string? Scenario { get; set; } = default;

        [Option('l', c_ListScenariosLongName, HelpText = "List the input project's scenarios, then exit (only valid with --" + c_InputLongName + ")")]
        public bool ListScenarios { get; set; } = default;



        [Option('o', c_OutputLongName, HelpText = "Output file path")]
        public string? OutputFilename { get; set; } = default;

        [Option('x', c_ExportLongName, HelpText = "Export file path - must end in .xlsx")]
        public string? ExportFilename { get; set; } = default;



        [Option('t', c_BaseThemeLongName, Default = BaseTheme.Light, Required = false, HelpText = "Output theme (Light|Dark)")]
        public BaseTheme BaseTheme { get; set; } = BaseTheme.Light;

        [Option('v', c_VerboseLongName, HelpText = "Show informational log output on stderr (warnings and errors always show)")]
        public bool Verbose { get; set; } = default;

        [Option(c_MetricsFormatLongName, Default = MetricsExport.Markdown, HelpText = "Metrics output format (Markdown|Table|Json)")]
        public MetricsExport MetricsFormat { get; set; } = default;



        [Option(c_GanttDirectoryLongName, HelpText = "Gantt chart output file directory")]
        public string? GanttDirectory { get; set; } = default;

        [Option(c_GanttFormatLongName, Default = PlotExport.Jpeg, HelpText = "Gantt chart format (Jpeg|Png|Bmp|Webp|Svg)")]
        public PlotExport GanttFormat { get; set; } = default;

        [Option(c_GanttSizeLongName, Min = 2, Max = 2, Separator = ':', HelpText = "Gantt chart dimensions in pixels (<width>:<height>)")]
        public IEnumerable<int> GanttSize { get; set; } = [];



        [Option(c_ArrowGraphDirectoryLongName, HelpText = "Arrow graph output file directory")]
        public string? ArrowGraphDirectory { get; set; } = default;

        [Option(c_ArrowGraphFormatLongName, Default = GraphExport.Jpeg, HelpText = "Arrow graph format (Jpeg|Png|Pdf|Svg|GraphML|Dot)")]
        public GraphExport ArrowGraphFormat { get; set; } = default;



        [Option(c_VertexGraphDirectoryLongName, HelpText = "Vertex graph output file directory")]
        public string? VertexGraphDirectory { get; set; } = default;

        [Option(c_VertexGraphFormatLongName, Default = GraphExport.Jpeg, HelpText = "Vertex graph format (Jpeg|Png|Pdf|Svg|GraphML|Dot)")]
        public GraphExport VertexGraphFormat { get; set; } = default;



        [Option(c_ResourceDirectoryLongName, HelpText = "Resource chart output file directory")]
        public string? ResourceDirectory { get; set; } = default;

        [Option(c_ResourceFormatLongName, Default = PlotExport.Jpeg, HelpText = "Resource chart format (Jpeg|Png|Bmp|Webp|Svg)")]
        public PlotExport ResourceFormat { get; set; } = default;

        [Option(c_ResourceSizeLongName, Min = 2, Max = 2, Separator = ':', HelpText = "Resource chart dimensions in pixels (<width>:<height>)")]
        public IEnumerable<int> ResourceSize { get; set; } = [];



        [Option(c_EVDirectoryLongName, HelpText = "Earned-Value chart output file directory")]
        public string? EVDirectory { get; set; } = default;

        [Option(c_EVFormatLongName, Default = PlotExport.Jpeg, HelpText = "Earned-Value chart format (Jpeg|Png|Bmp|Webp|Svg)")]
        public PlotExport EVFormat { get; set; } = default;

        [Option(c_EVSizeLongName, Min = 2, Max = 2, Separator = ':', HelpText = "Earned-Value chart dimensions in pixels (<width>:<height>)")]
        public IEnumerable<int> EVSize { get; set; } = [];



        [Option(c_ScenarioChartDirectoryLongName, HelpText = "Scenario chart output file directory")]
        public string? ScenarioChartDirectory { get; set; } = default;

        [Option(c_ScenarioChartFormatLongName, Default = PlotExport.Jpeg, HelpText = "Scenario chart format (Jpeg|Png|Bmp|Webp|Svg)")]
        public PlotExport ScenarioChartFormat { get; set; } = default;

        [Option(c_ScenarioChartSizeLongName, Min = 2, Max = 2, Separator = ':', HelpText = "Scenario chart dimensions in pixels (<width>:<height>)")]
        public IEnumerable<int> ScenarioChartSize { get; set; } = [];
    }
}
