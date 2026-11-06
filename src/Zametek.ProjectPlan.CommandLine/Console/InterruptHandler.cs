using System.Runtime.InteropServices;
using Serilog;

namespace Zametek.ProjectPlan.CommandLine
{
    // Ctrl+C (SIGINT) and SIGTERM stop a run of zpp in two steps. The first signal cancels the token of the run and says so: the
    // job then stops before the next step that it would take (a simulation stops between two chunks of its iterations), and zpp
    // ends with ExitCode.Cancelled. Files that the job has already written stay as they are, each of them whole, because the job
    // writes one output at a time and does not stop in the middle of one. The second signal is not handled, and so the system
    // ends the process at once, for a person who does not want to wait for the step that is in progress.
    internal sealed class InterruptHandler
        : IDisposable
    {
        private readonly CancellationTokenSource m_Cancellation;
        private readonly IJobConsole m_Console;
        private readonly List<PosixSignalRegistration> m_Registrations = [];

        private int m_Signals;

        public InterruptHandler(
            CancellationTokenSource cancellation,
            IJobConsole console)
        {
            ArgumentNullException.ThrowIfNull(cancellation);
            ArgumentNullException.ThrowIfNull(console);
            m_Cancellation = cancellation;
            m_Console = console;

            foreach (PosixSignal signal in new[] { PosixSignal.SIGINT, PosixSignal.SIGTERM })
            {
                try
                {
                    m_Registrations.Add(PosixSignalRegistration.Create(signal, context => context.Cancel = Handle()));
                }
                catch (Exception ex) when (ex is PlatformNotSupportedException or ArgumentException)
                {
                    // A platform that cannot tell zpp about the signal ends the process as it did before.
                    Log.Debug("The signal {Signal} cannot be handled: {Reason}", signal, ex.Message);
                }
            }
        }

        // The number of signals that came.
        public int Signals => Volatile.Read(ref m_Signals);

        // What a signal does. Returns whether the process goes on (the first signal), or whether the system is to end it (any
        // other). This is internal so that a test can send a signal without the system.
        internal bool Handle()
        {
            if (Interlocked.Increment(ref m_Signals) > 1)
            {
                return false;
            }

            try
            {
                // The message first, so that it comes before the message of the run that stops.
                m_Console.WriteErrorLineAsync(Resource.ProjectPlan.Messages.Message_RunStopping).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // A signal handler never throws: the run must stop whether or not the message was written.
                Log.Debug("The message about the signal was not written: {Reason}", ex.Message);
            }

            try
            {
                m_Cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The run has ended, and there is nothing left to stop.
            }

            return true;
        }

        public void Dispose()
        {
            foreach (PosixSignalRegistration registration in m_Registrations)
            {
                registration.Dispose();
            }

            m_Registrations.Clear();
        }
    }
}
