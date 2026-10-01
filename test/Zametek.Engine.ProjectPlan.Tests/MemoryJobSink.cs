namespace Zametek.Engine.ProjectPlan.Tests
{
    // Keeps a job's outputs and messages in memory, in the order the job hands them over - bytes in, bytes out, the
    // way a service would keep them for its response. An output named in FailingOutputs is refused, as a file that
    // cannot be written refuses zpp's. OnWrite sees each output as the job hands it over, before the sink takes it, so
    // a test can act on the job while that output is being produced.
    internal class MemoryJobSink
        : IJobSink
    {
        public List<(JobOutput Output, byte[] Content)> Outputs { get; } = [];

        public List<JobMessage> Messages { get; } = [];

        public HashSet<JobOutput> FailingOutputs { get; } = [];

        public Action<JobOutput>? OnWrite { get; init; }

        public byte[] this[JobOutput output] => Outputs.Single(x => x.Output == output).Content;

        public async Task WriteOutputAsync(JobOutput output, Func<Stream, Task> write)
        {
            OnWrite?.Invoke(output);

            if (FailingOutputs.Contains(output))
            {
                throw new IOException($@"{output} refused");
            }

            using var stream = new MemoryStream();
            await write(stream);
            Outputs.Add((output, stream.ToArray()));
        }

        public Task ReportAsync(JobMessage message)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
