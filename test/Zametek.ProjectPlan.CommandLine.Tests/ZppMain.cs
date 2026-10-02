namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // zpp's Main, as the tests run it: with an environment of its own - empty, unless a test gives it one - so that a
    // ZPP_SERVER or a ZPP_API_KEY set wherever the tests run cannot send their runs to a server.
    internal static class ZppMain
    {
        private static readonly IReadOnlyDictionary<string, string> s_NoEnvironment = new Dictionary<string, string>();

        public static Task<int> RunAsync(
            string[] args,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            return Program.MainAsync(args, environment ?? s_NoEnvironment);
        }

        // The same, with stdout and stderr captured. Main swaps the console's streams while it runs, so whatever calls
        // this is in ProgramExitCodeTests' collection.
        public static async Task<(int ExitCode, string Output, string Error)> RunCapturedAsync(
            string[] args,
            IReadOnlyDictionary<string, string>? environment = null)
        {
            TextWriter originalOutput = Console.Out;
            TextWriter originalError = Console.Error;

            try
            {
                using var output = new StringWriter();
                using var error = new StringWriter();
                Console.SetOut(output);
                Console.SetError(error);

                int exitCode = await RunAsync(args, environment);
                return (exitCode, output.ToString(), error.ToString());
            }
            finally
            {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
            }
        }
    }
}
