using Zametek.ProjectPlan.Engine;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // zpp's end of a job: each output is written to the file the options name for it, as soon as the job produces it,
    // and each message is printed as the desktop would show it in a dialog - errors and warnings on stderr, anything
    // else on stdout.
    internal class FileJobSink
        : IJobSink
    {
        #region Fields

        private readonly IReadOnlyDictionary<JobOutput, string> m_Filenames;

        #endregion

        #region Ctors

        public FileJobSink(IReadOnlyDictionary<JobOutput, string> filenames)
        {
            ArgumentNullException.ThrowIfNull(filenames);
            m_Filenames = filenames;
        }

        #endregion

        #region IJobSink Members

        public async Task WriteOutputAsync(JobOutput output, Func<Stream, Task> write)
        {
            await FileStreamHelper.SaveAsync(m_Filenames[output], write);
        }

        public async Task ReportAsync(JobMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);

            string text = $@"{message.Title}: {message.Message}";

            if (message.Kind is JobMessageKind.Error or JobMessageKind.Warning)
            {
                await Console.Error.WriteLineAsync(text);
            }
            else
            {
                await StandardOutput.WriteLineAsync(text);
            }
        }

        #endregion
    }
}
