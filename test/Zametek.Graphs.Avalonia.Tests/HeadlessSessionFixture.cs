using Avalonia.Headless;
using System.Reflection;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests
{
    // One shared headless Avalonia session (a single UI thread) for all rendering tests. Avalonia is
    // single-UI-thread per process, so every Avalonia-touching test must marshal onto this one session's
    // thread; creating a session per test puts objects (e.g. the font manager) on different threads and
    // trips the cross-thread access checks.
    public sealed class HeadlessSessionFixture : IDisposable
    {
        // Avalonia 12.1.1 races while it starts a session: StartNew hands the session its dispatch task from a
        // variable that the new dispatch thread can read before StartNew has assigned it. When the thread gets
        // there first - which takes a busy machine - the session holds no task, and Dispose throws a
        // NullReferenceException where it waits for it, after it has already stopped the dispatch loop. xUnit
        // reports that as a collection cleanup failure, which fails the run although every test passed. Fixed
        // upstream in 12.1.3 (AvaloniaUI/Avalonia#22222): delete this and the catch below once the solution is on
        // it.
        private static readonly FieldInfo? s_DispatchTask =
            typeof(HeadlessUnitTestSession).GetField(@"_dispatchTask", BindingFlags.NonPublic | BindingFlags.Instance);

        public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));

        public void Dispose()
        {
            try
            {
                Session.Dispose();
            }
            catch (NullReferenceException) when (HasNoDispatchTask())
            {
            }
        }

        // True only in the state the race leaves behind, so that any other failure to dispose still fails the run.
        private bool HasNoDispatchTask()
        {
            return s_DispatchTask is not null
                && s_DispatchTask.GetValue(Session) is null;
        }
    }

    [CollectionDefinition("Headless rendering")]
    public sealed class HeadlessRenderingCollection : ICollectionFixture<HeadlessSessionFixture>
    {
    }
}
