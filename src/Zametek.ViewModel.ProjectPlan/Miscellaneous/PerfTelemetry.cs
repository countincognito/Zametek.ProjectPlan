using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Dormant, reusable performance telemetry. Writes timestamped,
    /// thread-stamped event lines to %TEMP%\ProjectPlanPerf_{pid}.log through a
    /// background queue, so the cost on an instrumented path is one string
    /// format plus one queue add; nothing runs and no file is created until the
    /// first call site executes. To use: add temporary <see cref="Mark"/> and
    /// <see cref="Measure"/> call sites over the code under investigation, call
    /// <see cref="RegisterUiThread"/> from application start-up so UI-thread
    /// lines gain a trailing '*' on the thread column, and (optionally) run an
    /// input-priority DispatcherTimer heartbeat that calls Mark whenever the
    /// tick gap exceeds a threshold, which makes UI-thread stalls visible in
    /// the timeline. First used for the 2026-08 edit-cascade freeze
    /// investigation.
    /// </summary>
    public static class PerfTelemetry
    {
        private static readonly Stopwatch s_Clock = Stopwatch.StartNew();
        private static readonly BlockingCollection<string> s_Queue = new();
        private static volatile int s_UiThreadId = -1;

        public static string LogFilePath { get; } =
            Path.Combine(Path.GetTempPath(), $@"ProjectPlanPerf_{Environment.ProcessId}.log");

        static PerfTelemetry()
        {
            var writerThread = new Thread(WriteQueueToFile)
            {
                IsBackground = true,
                Name = @"PerfTelemetryWriter",
            };
            writerThread.Start();
            Mark($@"=== PerfTelemetry session {DateTime.Now:O} pid={Environment.ProcessId} ===");
        }

        public static void RegisterUiThread()
        {
            s_UiThreadId = Environment.CurrentManagedThreadId;
            Mark(@"UI thread registered");
        }

        public static void Mark(string message)
        {
            int threadId = Environment.CurrentManagedThreadId;
            char uiMarker = threadId == s_UiThreadId ? '*' : ' ';
            s_Queue.Add(string.Create(
                CultureInfo.InvariantCulture,
                $@"{DateTime.Now:HH:mm:ss.fff} | {s_Clock.Elapsed.TotalMilliseconds,12:F2} | T{threadId,3}{uiMarker} | {message}"));
        }

        public static IDisposable Measure(string name)
        {
            Mark($@"{name} BEGIN");
            return new MeasureScope(name, s_Clock.Elapsed);
        }

        private static void WriteQueueToFile()
        {
            try
            {
                using var writer = new StreamWriter(LogFilePath, append: false)
                {
                    AutoFlush = true,
                };

                foreach (string line in s_Queue.GetConsumingEnumerable())
                {
                    writer.WriteLine(line);
                }
            }
            catch
            {
                // Telemetry must never take the app down; on any failure keep
                // draining the queue so instrumented paths do not accumulate
                // memory.
                foreach (string _ in s_Queue.GetConsumingEnumerable())
                {
                }
            }
        }

        private sealed class MeasureScope(
            string name,
            TimeSpan begin)
            : IDisposable
        {
            public void Dispose() => Mark($@"{name} END after {(s_Clock.Elapsed - begin).TotalMilliseconds:F2}ms");
        }
    }
}
