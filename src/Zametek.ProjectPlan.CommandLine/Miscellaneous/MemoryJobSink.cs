using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp serve's end of a job: each output is kept in memory for the response, in the order the job produces them, and
    // each message is printed on the job's console, as zpp prints it.
    internal class MemoryJobSink
        : IJobSink
    {
        #region Fields

        private readonly Lock m_Lock = new();
        private readonly List<(JobOutput Output, byte[] Content)> m_Outputs = [];
        private readonly IJobConsole m_Console;

        #endregion

        #region Ctors

        public MemoryJobSink(IJobConsole console)
        {
            ArgumentNullException.ThrowIfNull(console);
            m_Console = console;
        }

        #endregion

        #region Properties

        public IReadOnlyList<(JobOutput Output, byte[] Content)> Outputs
        {
            get
            {
                lock (m_Lock)
                {
                    return [.. m_Outputs];
                }
            }
        }

        #endregion

        #region IJobSink Members

        public async Task WriteOutputAsync(JobOutput output, Func<Stream, Task> write)
        {
            ArgumentNullException.ThrowIfNull(write);

            // Rendered in full before it is kept, so that an output that fails part way is not kept at all.
            using var stream = new MemoryStream();
            await write(stream);
            byte[] content = stream.ToArray();

            lock (m_Lock)
            {
                m_Outputs.Add((output, content));
            }
        }

        public async Task ReportAsync(JobMessage message)
        {
            await JobConsoleHelper.WriteMessageAsync(m_Console, message);
        }

        #endregion
    }
}
