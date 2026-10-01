namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // Keeps each call a run makes on its console, in the order it makes them - which stream the text went to and, for a
    // block it displayed, whether the block reported errors - so a test can check what was printed, and how, without
    // touching the process's own console.
    internal class RecordingJobConsole
        : IJobConsole
    {
        public List<Call> Calls { get; } = [];

        public Task WriteLineAsync(string text)
        {
            Calls.Add(Line(text));
            return Task.CompletedTask;
        }

        public Task DisplayAsync(string content, bool hasErrors)
        {
            Calls.Add(Display(content, hasErrors));
            return Task.CompletedTask;
        }

        public Task WriteErrorLineAsync(string text)
        {
            Calls.Add(ErrorLine(text));
            return Task.CompletedTask;
        }

        public static Call Line(string text) => new(nameof(WriteLineAsync), text, HasErrors: false);

        public static Call Display(string content, bool hasErrors) => new(nameof(DisplayAsync), content, hasErrors);

        public static Call ErrorLine(string text) => new(nameof(WriteErrorLineAsync), text, HasErrors: false);

        // One call on the console: the method, the text it was given, and whether it reported errors.
        public record Call(
            string Method,
            string Text,
            bool HasErrors);
    }
}
