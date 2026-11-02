using Avalonia.Threading;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Dormant, reusable tracer for questions about threads: on which thread does a value reach the UI, and does the
    /// UI object that receives it throw? It writes through <see cref="PerfTelemetry"/>, to
    /// %TEMP%\ProjectPlanPerf_{pid}.log, where a line from the UI thread has a '*' after its thread number.
    /// Nothing runs, and no file is made, until <see cref="Enable"/> is called, so the call sites that stay in the
    /// code cost a flag test, and the tests and the jobs of zpp write no file. To use it:
    /// <list type="number">
    /// <item>Call <see cref="Enable"/> first on the UI thread, for example at the start of
    /// <c>App.OnFrameworkInitializationCompleted</c>.</item>
    /// <item>Wrap the code under investigation with <see cref="Publish"/>, or add <see cref="Mark"/> lines, for
    /// example in the handler of a property of a view (<c>OnPropertyChanged</c>) to see where it runs.</item>
    /// <item>Run the application, do the steps, and read the file.</item>
    /// </list>
    /// First used on 2026-10-17, to check whether a chart view throws when its plot is published from a thread of the
    /// pool. It does not: the log of the real application showed the view handling the change on the UI thread
    /// (see ARCHITECTURE section 12). A headless test had shown an exception, and thus a headless test does not
    /// predict the behaviour of the bindings of the real application.
    /// </summary>
    public static class ThreadTrace
    {
        private static volatile bool s_Enabled;

        public static void Enable()
        {
            s_Enabled = true;
            PerfTelemetry.RegisterUiThread();
            PerfTelemetry.Mark(@"ThreadTrace enabled: a '*' after the thread number means the UI thread");
        }

        public static void Mark(string message)
        {
            if (s_Enabled)
            {
                PerfTelemetry.Mark(message);
            }
        }

        /// <summary>
        /// Runs an action that hands a value to the UI, and says on which thread it ran, whether that thread may touch
        /// the UI, and whether it threw (and what). The exception is thrown again, so the behaviour of the application
        /// does not change.
        /// </summary>
        public static void Publish(string name, Action publish)
        {
            ArgumentNullException.ThrowIfNull(publish);

            if (!s_Enabled)
            {
                publish();
                return;
            }

            PerfTelemetry.Mark($@"{name}: publishing (UI thread access: {Dispatcher.UIThread.CheckAccess()})");

            try
            {
                publish();
                PerfTelemetry.Mark($@"{name}: published without an exception");
            }
            catch (Exception ex)
            {
                PerfTelemetry.Mark($@"{name}: publishing THREW {ex.GetType().Name}: {ex.Message}");
                throw;
            }
        }
    }
}
