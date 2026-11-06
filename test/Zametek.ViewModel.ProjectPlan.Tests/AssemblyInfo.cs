using Xunit;

// These tests install a sequencer into RxSchedulers.MainThreadScheduler, which is
// process-global: a test running in parallel would have its own deferred deliveries
// queued into another test's pump and never drained. Serialising the assembly costs
// almost nothing here (the whole project runs in a couple of seconds) and matches
// what Zametek.Graphs.Avalonia.Tests already does for its headless session.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
