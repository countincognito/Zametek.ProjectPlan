namespace Zametek.ProjectPlan.Engine
{
    // Where a job's outputs and messages go. The engine produces them and the host decides what becomes of them: zpp
    // writes each output to the file its options name and prints each message, and a service would keep them for its
    // response. The engine hands each one over as soon as it is produced, in the order the job produces them.
    public interface IJobSink
    {
        // Stores one of the job's outputs. write renders the output into whichever stream it is given; the sink
        // decides where the bytes go, and throws if they cannot be stored there.
        Task WriteOutputAsync(JobOutput output, Func<Stream, Task> write);

        // Reports a message the job raised as it ran: one the desktop would show in a dialog.
        Task ReportAsync(JobMessage message);
    }
}
