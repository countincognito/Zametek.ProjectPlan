using CommandLine;

namespace Zametek.ProjectPlan.CommandLine
{
    public class Options
    {
        [Option('v', "verbose", Required = false, HelpText = "Enable verbose output.")]
        public bool Verbose { get; set; }

        [Option('i', "input", Required = true, HelpText = "Input file path.")]
        public string InputFilePath { get; set; }

        [Option('o', "output", Required = false, HelpText = "Output file path.")]
        public string OutputFilePath { get; set; }
    }
}
