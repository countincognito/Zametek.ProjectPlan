using System.Text;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // A job's console on zpp serve: it keeps what the job prints, for the response, in place of zpp's stdout and
    // stderr. Its text is what zpp's own console prints, except that lines on stderr end with NewLineHelper.NewLine
    // too, so that a response says the same on every platform; and it has no colours.
    internal class BufferedConsole
        : IJobConsole
    {
        #region Fields

        private readonly Lock m_Lock = new();
        private readonly StringBuilder m_Output = new();
        private readonly StringBuilder m_Error = new();

        #endregion

        #region Properties

        // What the job printed on stdout.
        public string Output
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Output.ToString();
                }
            }
        }

        // What the job printed on stderr.
        public string Error
        {
            get
            {
                lock (m_Lock)
                {
                    return m_Error.ToString();
                }
            }
        }

        #endregion

        #region IJobConsole Members

        public Task WriteLineAsync(string text)
        {
            string line = NewLineHelper.NormalizeNewLines(text) + NewLineHelper.NewLine;

            lock (m_Lock)
            {
                m_Output.Append(line);
            }
            return Task.CompletedTask;
        }

        public Task DisplayAsync(string content, bool hasErrors)
        {
            string block = NewLineHelper.NewLine + NewLineHelper.NormalizeNewLines(content) + NewLineHelper.NewLine;

            lock (m_Lock)
            {
                m_Output.Append(block);
            }
            return Task.CompletedTask;
        }

        public Task WriteErrorLineAsync(string text)
        {
            string line = NewLineHelper.NormalizeNewLines(text) + NewLineHelper.NewLine;

            lock (m_Lock)
            {
                m_Error.Append(line);
            }
            return Task.CompletedTask;
        }

        #endregion
    }
}
