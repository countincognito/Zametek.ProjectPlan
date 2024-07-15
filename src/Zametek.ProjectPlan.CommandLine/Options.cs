using CommandLine;

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



        [Option('c', "compile", Required = false, HelpText = "Compile incoming file.")]
        public bool Compile { get; set; } = false;



        //[Option("gantt", SetName = "gantt", HelpText = "Gantt chart.")]
        //public bool Gantt { get; set; } = false;

        [Option("gantt-format", SetName = "gantt", HelpText = "Gantt chart format.")]
        public string? GanttFormat { get; set; } = default;

        [Option("gantt-group", SetName = "gantt", HelpText = "Gantt chart group.")]
        public string? GanttGroup { get; set; } = default;

        [Option("gantt-size", SetName = "gantt", Max = 2, Separator = ':', HelpText = "Gantt chart parameters width and height (px).")]
        public IEnumerable<int> GanttSize { get; set; } = [];








    }
}
