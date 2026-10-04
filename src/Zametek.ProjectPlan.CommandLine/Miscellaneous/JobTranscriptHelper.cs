using Zametek.Engine.ProjectPlan;

namespace Zametek.ProjectPlan.CommandLine
{
    // Plays the transcript of a job a server ran back through zpp's own console and file sink: it prints what the job
    // printed and writes each output to the file zpp would have written it to, in the order the job did - and it fails
    // where zpp would have. A project or an export that cannot be written stops the run, as it stops a job; a chart or a
    // graph that cannot be written is reported as a job reports one, and the run carries on, to end as a failure.
    internal static class JobTranscriptHelper
    {
        // Whether the transcript can be played back as the answer to the job zpp sent: each line and block it records
        // has its text, and each output it records is one of the answer's, with its content, and one the job asked for.
        // A transcript that cannot be played back is refused before anything of it is printed or written.
        public static bool IsPlayable(
            IReadOnlyList<JobTranscriptEntry>? transcript,
            IReadOnlyList<OutputResponse>? outputs,
            IReadOnlyDictionary<JobOutput, string> filenames)
        {
            ArgumentNullException.ThrowIfNull(filenames);

            if (transcript is null
                || outputs is null)
            {
                return false;
            }

            return transcript.All(entry => entry.Kind switch
            {
                JobTranscriptKind.Line or JobTranscriptKind.Display or JobTranscriptKind.ErrorLine => entry.Text is not null,
                JobTranscriptKind.Output => entry.Index is int index
                    && index >= 0
                    && index < outputs.Count
                    && outputs[index].Content is not null
                    && filenames.ContainsKey(outputs[index].Kind),
                _ => false,
            });
        }

        // Plays back a transcript IsPlayable has passed, and returns the exit code zpp ends the run with: the job's,
        // unless a chart or a graph could not be written here, when the run, as a whole, has failed.
        public static async Task<ExitCode> PlayAsync(
            IReadOnlyList<JobTranscriptEntry> transcript,
            IReadOnlyList<OutputResponse> outputs,
            ExitCode exitCode,
            IJobConsole console,
            IJobSink sink)
        {
            ArgumentNullException.ThrowIfNull(transcript);
            ArgumentNullException.ThrowIfNull(outputs);
            ArgumentNullException.ThrowIfNull(console);
            ArgumentNullException.ThrowIfNull(sink);

            bool hasFailedOutputs = false;

            foreach (JobTranscriptEntry entry in transcript)
            {
                switch (entry.Kind)
                {
                    case JobTranscriptKind.Line:
                        await console.WriteLineAsync(entry.Text ?? throw new InvalidOperationException());
                        break;
                    case JobTranscriptKind.Display:
                        await console.DisplayAsync(entry.Text ?? throw new InvalidOperationException(), entry.HasErrors);
                        break;
                    case JobTranscriptKind.ErrorLine:
                        // The transcript ends its lines with \n, and zpp ends stderr's as the platform does.
                        await console.WriteErrorLineAsync((entry.Text ?? throw new InvalidOperationException()).ReplaceLineEndings(Environment.NewLine));
                        break;
                    case JobTranscriptKind.Output:
                        OutputResponse output = outputs[entry.Index ?? throw new InvalidOperationException()];
                        hasFailedOutputs |= !await WriteOutputAsync(output, sink);
                        break;
                }
            }

            return hasFailedOutputs && exitCode == ExitCode.Success ? ExitCode.Failure : exitCode;
        }

        // Writes an output as a job's sink is given it, and says whether it could be.
        private static async Task<bool> WriteOutputAsync(
            OutputResponse output,
            IJobSink sink)
        {
            byte[] content = output.Content ?? throw new InvalidOperationException();
            async Task Write(Stream stream) => await stream.WriteAsync(content);

            // As in a job, either of these failing stops the run.
            if (output.Kind is JobOutput.Project or JobOutput.ScenarioExport)
            {
                await sink.WriteOutputAsync(output.Kind, Write);
                return true;
            }

            try
            {
                await sink.WriteOutputAsync(output.Kind, Write);
                return true;
            }
            catch (Exception ex)
            {
                await sink.ReportAsync(new JobMessage(JobMessageKind.Error, Resource.ProjectPlan.Titles.Title_Error, string.Empty, ex.Message));
                return false;
            }
        }
    }
}
