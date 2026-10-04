using Microsoft.Extensions.Logging;

namespace Zametek.ProjectPlan.CommandLine.Tests
{
    // A logger that keeps what it is told, for a test of what a class logs: each entry's level and its message as it would have been
    // written.
    internal sealed class RecordingLogger<T>
        : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
