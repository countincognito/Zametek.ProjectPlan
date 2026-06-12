using System.Collections.Concurrent;
using System.Diagnostics;

namespace Zametek.ViewModel.ProjectPlan
{
    // TODO: Temporary diagnostics for measuring redundant Build* cascades across the
    // manager view models during project scenario loading/resetting. Remove this class
    // (and its call sites) once the IsBulkUpdating gating work has been verified.
    internal static class CascadeDiagnostics
    {
        private static readonly ConcurrentDictionary<string, int> s_Counts = new();

        [Conditional("DEBUG")]
        public static void RecordBuild(string name)
        {
            int count = s_Counts.AddOrUpdate(name, 1, (_, current) => current + 1);
            Debug.WriteLine($"[CascadeDiagnostics] {DateTime.Now:HH:mm:ss.fff} {name} call #{count}");
        }

        [Conditional("DEBUG")]
        public static void RecordMarker(string message)
        {
            Debug.WriteLine($"[CascadeDiagnostics] {DateTime.Now:HH:mm:ss.fff} ===== {message} =====");
        }

        [Conditional("DEBUG")]
        public static void RecordStackTrace(string message)
        {
            Debug.WriteLine($"[CascadeDiagnostics] {DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}{Environment.StackTrace}");
        }
    }
}
