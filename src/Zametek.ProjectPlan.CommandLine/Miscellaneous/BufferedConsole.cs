using System.Text;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // A job's console on zpp serve: it keeps what the job prints, for the response, in place of zpp's stdout and
    // stderr. Its text is what zpp's own console prints, except that lines on stderr end with NewLineHelper.NewLine
    // too, so that a response says the same on every platform; and it has no colours. It also keeps the job's
    // transcript - each thing the job printed, and each output its sink kept, in the order they came - from which a
    // client can print and write what zpp would have, colours and all.
    internal class BufferedConsole
        : IJobConsole
    {
        #region Fields

        private readonly Lock m_Lock = new();
        private readonly StringBuilder m_Output = new();
        private readonly StringBuilder m_Error = new();
        private readonly List<JobTranscriptEntry> m_Transcript = [];

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

        // What the job printed and produced, in the order it did.
        public IReadOnlyList<JobTranscriptEntry> Transcript
        {
            get
            {
                lock (m_Lock)
                {
                    return [.. m_Transcript];
                }
            }
        }

        #endregion

        #region Public Members

        // Records that the job produced an output: the one at index in the outputs its sink keeps.
        public void RecordOutput(int index)
        {
            lock (m_Lock)
            {
                m_Transcript.Add(new JobTranscriptEntry { Kind = JobTranscriptKind.Output, Index = index });
            }
        }

        #endregion

        #region IJobConsole Members

        public Task WriteLineAsync(string text)
        {
            string normalized = NewLineHelper.NormalizeNewLines(text);

            lock (m_Lock)
            {
                m_Output.Append(normalized + NewLineHelper.NewLine);
                m_Transcript.Add(new JobTranscriptEntry { Kind = JobTranscriptKind.Line, Text = normalized });
            }
            return Task.CompletedTask;
        }

        public Task DisplayAsync(string content, bool hasErrors)
        {
            string normalized = NewLineHelper.NormalizeNewLines(content);

            lock (m_Lock)
            {
                m_Output.Append(NewLineHelper.NewLine + normalized + NewLineHelper.NewLine);
                m_Transcript.Add(new JobTranscriptEntry { Kind = JobTranscriptKind.Display, Text = normalized, HasErrors = hasErrors });
            }
            return Task.CompletedTask;
        }

        public Task WriteErrorLineAsync(string text)
        {
            string normalized = NewLineHelper.NormalizeNewLines(text);

            lock (m_Lock)
            {
                m_Error.Append(normalized + NewLineHelper.NewLine);
                m_Transcript.Add(new JobTranscriptEntry { Kind = JobTranscriptKind.ErrorLine, Text = normalized });
            }
            return Task.CompletedTask;
        }

        #endregion
    }
}
