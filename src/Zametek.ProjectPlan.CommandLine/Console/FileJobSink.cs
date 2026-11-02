using Zametek.Engine.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp's end of a job: each output is written to the file the options name for it, as soon as the job produces it,
    // and each message is printed on zpp's console as the desktop would show it in a dialog - errors and warnings on
    // stderr, anything else on stdout.
    internal class FileJobSink
        : IJobSink
    {
        #region Fields

        private readonly IReadOnlyDictionary<JobOutput, string> m_Filenames;
        private readonly IJobConsole m_Console;

        #endregion

        #region Ctors

        public FileJobSink(
            IReadOnlyDictionary<JobOutput, string> filenames,
            IJobConsole console)
        {
            ArgumentNullException.ThrowIfNull(filenames);
            ArgumentNullException.ThrowIfNull(console);
            m_Filenames = filenames;
            m_Console = console;
        }

        #endregion

        #region IJobSink Members

        public async Task WriteOutputAsync(JobOutput output, Func<Stream, Task> write)
        {
            await FileStreamHelper.SaveAsync(m_Filenames[output], write);
        }

        public async Task ReportAsync(JobMessage message)
        {
            await JobConsoleHelper.WriteMessageAsync(m_Console, message);
        }

        #endregion
    }
}
