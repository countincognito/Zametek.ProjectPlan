using CommandLine;
using Zametek.Common.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    public class Options
    {
        [Option('i', "input", Group = "file-in", HelpText = "Input file path.")]
        public string? InputFilename { get; set; } = default;

        [Option('m', "import", Group = "file-in", HelpText = "Import file path.")]
        public string? ImportFilename { get; set; } = default;



        [Option('o', "output", Group = "file-out", HelpText = "Output file path.")]
        public string? OutputFilename { get; set; } = default;

        [Option('x', "export", Group = "file-out", HelpText = "Export file path.")]
        public string? ExportFilename { get; set; } = default;



        [Option('c', "compile", Default = false, Required = false, HelpText = "Compile incoming file.")]
        public bool Compile { get; set; } = false;



        [Option("gantt-output", HelpText = "Gantt output file path.")]
        public string? GanttOutput { get; set; } = default;

        [Option("gantt-format", Default = PlotExport.Jpeg, HelpText = "Gantt chart format (Jpeg|Png|Pdf)")]
        public PlotExport GanttFormat { get; set; } = default;

        [Option("gantt-group", Default = GroupByMode.None, HelpText = "Gantt chart group (None|Resource|WorkStream)")]
        public GroupByMode GanttGroup { get; set; } = default;

        [Option("gantt-size", Max = 2, Separator = ':', HelpText = "Gantt chart parameters width and height (px).")]
        public IEnumerable<int> GanttSize { get; set; } = [];








    }
}
