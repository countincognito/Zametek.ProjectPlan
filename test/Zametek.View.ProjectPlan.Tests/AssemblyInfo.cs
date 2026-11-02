using Xunit;

// One application, with one composition root and one UI thread, serves all the tests.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
