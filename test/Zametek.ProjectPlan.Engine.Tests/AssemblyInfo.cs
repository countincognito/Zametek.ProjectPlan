using Xunit;

// Jobs are run one at a time until the engine is made safe to run them side by side (the next phase of the headless
// service work): some of what they share is process-wide, such as the colour sequence resource scheduling hands out.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
