using CommandLine;

namespace Zametek.ProjectPlan.CommandLine
{

    [Verb("export", isDefault: false, HelpText = "")]
    public class ExportOptions
    {
        [Option('v', "verbose", Required = false, HelpText = "Enable verbose output.")]
        public bool Verbose { get; set; }

        [Option('i', "input", Required = true, HelpText = "Input file path.")]
        public string? InputFilename { get; set; } = default;

        [Option('o', "output", Required = true, HelpText = "Output file path.")]
        public string? OutputFilename { get; set; } = default;
    }
}
