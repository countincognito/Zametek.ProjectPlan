using Xunit;

// The rendering tests spin up a headless Avalonia session and touch shared SkiaSharp font state; run the
// whole assembly serially so those do not race with each other or the other tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
