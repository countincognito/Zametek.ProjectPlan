using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.Diagnostics;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Dormant diagnostics for tracing the reactive Build* cascades across the
    /// manager view models (e.g. during project scenario loading/resetting), used
    /// to verify the IsBulkUpdating gating work and diagnose redundant rebuilds,
    /// spurious compiles, and deadlocks.
    ///
    /// The methods are gated on the CASCADE_DIAGNOSTICS compilation symbol, which
    /// is not normally defined, so the compiler strips every call site entirely
    /// (including argument evaluation) - the hooks cost nothing at runtime. All
    /// call sites live in this assembly, so to re-enable them define the symbol
    /// for this project only, by temporarily adding to a PropertyGroup in
    /// Zametek.ViewModel.ProjectPlan.csproj:
    ///
    ///   &lt;DefineConstants&gt;$(DefineConstants);CASCADE_DIAGNOSTICS&lt;/DefineConstants&gt;
    ///
    /// or from the command line (DEBUG and TRACE must be restated because the
    /// command-line property replaces the whole list, and the output below relies
    /// on DEBUG; the semicolons must be escaped as %3B):
    ///
    ///   dotnet build -p:DefineConstants="DEBUG%3BTRACE%3BCASCADE_DIAGNOSTICS"
    ///
    /// Output goes to Debug.WriteLine (itself compiled away outside Debug builds)
    /// and is mirrored to zametek-cascade-diagnostics.log in the user's temp
    /// directory, so a Debug build can be run normally - no debugger attached -
    /// and the trace collected from the file afterwards. The file is appended
    /// across runs, with a process banner separating each run.
    /// </summary>
    internal static class CascadeDiagnostics
    {
        private static readonly ConcurrentDictionary<string, int> s_Counts = new();
        private static int s_Sequence;

        // The file mirror. Created lazily on the first record call, so a build
        // without the symbol never touches the file system; deliberately never
        // disposed, because it must live for the whole process (AutoFlush lands
        // each line as it is written). If the file cannot be opened - e.g. a
        // second app instance already holds it - the mirror silently drops out
        // and output continues through Debug.WriteLine alone.
        private static readonly Lazy<TextWriter?> s_LogWriter = new(() =>
        {
            try
            {
                string path = Path.Combine(Path.GetTempPath(), "zametek-cascade-diagnostics.log");
                var writer = new StreamWriter(path, append: true)
                {
                    AutoFlush = true,
                };
                writer.WriteLine($"===== process {Environment.ProcessId} started {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====");
                return TextWriter.Synchronized(writer);
            }
            catch
            {
                return null;
            }
        });

        private static void WriteLine(string line)
        {
            Debug.WriteLine(line);
            s_LogWriter.Value?.WriteLine(line);
        }

        /// <summary>
        /// Builds the line prefix: a global sequence number and the managed
        /// thread id ahead of the wall-clock time. The sequence number gives a
        /// total order over all diagnostic calls - cross-thread ordering of
        /// racing writes cannot be reconstructed from millisecond timestamps
        /// alone - and the thread id shows which side of a race each line
        /// came from.
        /// </summary>
        private static string Stamp() =>
            $"[CascadeDiagnostics] #{Interlocked.Increment(ref s_Sequence):D6} T{Environment.CurrentManagedThreadId:D3} {DateTime.Now:HH:mm:ss.fff}";

        /// <summary>
        /// Records one invocation of a (re)build step, with a running call
        /// count per name. Use it on Build*/Refresh* cascade entry points to
        /// spot redundant or duplicated rebuilds - the count is the point.
        /// </summary>
        [Conditional("CASCADE_DIAGNOSTICS")]
        public static void RecordBuild(string name)
        {
            int count = s_Counts.AddOrUpdate(name, 1, (_, current) => current + 1);
            WriteLine($"{Stamp()} {name} call #{count}");
        }

        /// <summary>
        /// Records a section boundary (banner-decorated with =====) marking
        /// the start or end of a phase, e.g. a bulk update or the save
        /// milestones. Markers act as headings when scanning a long trace;
        /// the individual observations between them belong in
        /// <see cref="RecordEvent"/>.
        /// </summary>
        [Conditional("CASCADE_DIAGNOSTICS")]
        public static void RecordMarker(string message)
        {
            WriteLine($"{Stamp()} ===== {message} =====");
        }

        /// <summary>
        /// Records a single plain observation - a data point such as a
        /// property write with its old and new values. This is the workhorse
        /// for tracing cross-thread ordering; use <see cref="RecordMarker"/>
        /// for the phase boundaries around these lines.
        /// </summary>
        [Conditional("CASCADE_DIAGNOSTICS")]
        public static void RecordEvent(string message)
        {
            WriteLine($"{Stamp()} {message}");
        }

        /// <summary>
        /// Records a CollectionChanged notification raised by a bound view
        /// collection, together with the collection's resulting order: exactly
        /// what a bound control was told (action and indices) and what the
        /// data layer holds afterwards. A display that disagrees with the
        /// logged order convicts the control, not the data pipeline.
        /// </summary>
        [Conditional("CASCADE_DIAGNOSTICS")]
        public static void RecordCollectionChange(
            string tag,
            NotifyCollectionChangedEventArgs args,
            IEnumerable<string> resultingOrder)
        {
            WriteLine($"{Stamp()} {tag} {args.Action} newIndex={args.NewStartingIndex} oldIndex={args.OldStartingIndex} new=[{FormatNames(args.NewItems)}] old=[{FormatNames(args.OldItems)}] order=[{string.Join(", ", resultingOrder)}]");
        }

        private static string FormatNames(IList? items) =>
            items is null
                ? string.Empty
                : string.Join(", ", items.Cast<object?>().Select(x => x is IManagedNodeViewModel node ? node.Name : x?.ToString() ?? "<null>"));

        /// <summary>
        /// Records a message together with the full stack trace of the
        /// caller, for provenance: not just that a transition happened but
        /// which code path triggered it. Expensive and noisy, so reserve it
        /// for rare transitions (e.g. flag edges), not per-item events.
        /// </summary>
        [Conditional("CASCADE_DIAGNOSTICS")]
        public static void RecordStackTrace(string message)
        {
            WriteLine($"{Stamp()} {message}{Environment.NewLine}{Environment.StackTrace}");
        }
    }
}
