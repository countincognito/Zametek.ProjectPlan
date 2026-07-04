using Avalonia.Headless;
using Xunit;

namespace Zametek.Graphs.Avalonia.Tests
{
    // One shared headless Avalonia session (a single UI thread) for all rendering tests. Avalonia is
    // single-UI-thread per process, so every Avalonia-touching test must marshal onto this one session's
    // thread; creating a session per test puts objects (e.g. the font manager) on different threads and
    // trips the cross-thread access checks.
    public sealed class HeadlessSessionFixture : IDisposable
    {
        public HeadlessUnitTestSession Session { get; } = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));

        public void Dispose() => Session.Dispose();
    }

    [CollectionDefinition("Headless rendering")]
    public sealed class HeadlessRenderingCollection : ICollectionFixture<HeadlessSessionFixture>
    {
    }
}
