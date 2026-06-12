using System.Collections.Concurrent;
using System.Diagnostics;

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
    /// Output goes to Debug.WriteLine (itself compiled away outside Debug builds),
    /// so use a Debug build and watch the debugger output window.
    /// </summary>
    internal static class CascadeDiagnostics
    {
        private static readonly ConcurrentDictionary<string, int> s_Counts = new();

        [Conditional("CASCADE_DIAGNOSTICS")]
        public static void RecordBuild(string name)
        {
            int count = s_Counts.AddOrUpdate(name, 1, (_, current) => current + 1);
            Debug.WriteLine($"[CascadeDiagnostics] {DateTime.Now:HH:mm:ss.fff} {name} call #{count}");
        }

        [Conditional("CASCADE_DIAGNOSTICS")]
        public static void RecordMarker(string message)
        {
            Debug.WriteLine($"[CascadeDiagnostics] {DateTime.Now:HH:mm:ss.fff} ===== {message} =====");
        }

        [Conditional("CASCADE_DIAGNOSTICS")]
        public static void RecordStackTrace(string message)
        {
            Debug.WriteLine($"[CascadeDiagnostics] {DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}{Environment.StackTrace}");
        }
    }
}
