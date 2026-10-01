namespace Zametek.ProjectPlan.CommandLine
{
    // Where the text zpp prints for a run goes: its stdout and its stderr. zpp prints it on its own console, line by line
    // as the run goes; a service would keep each job's text for its response.
    internal interface IJobConsole
    {
        // Writes a line to stdout.
        Task WriteLineAsync(string text);

        // Writes a block of text to stdout, after a blank line that sets it apart from whatever came before. hasErrors
        // says whether the block reports errors, which a console may show differently.
        Task DisplayAsync(string content, bool hasErrors);

        // Writes a line to stderr.
        Task WriteErrorLineAsync(string text);
    }
}
