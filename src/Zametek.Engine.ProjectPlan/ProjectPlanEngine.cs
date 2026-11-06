using ReactiveUI.Builder;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.Engine.ProjectPlan
{
    // The process-wide set-up that every job depends on. A host calls Initialize before it builds its container; it
    // does the work only once per process, so a host that is itself run repeatedly in one process - zpp's tests run
    // its Main over and over - can call it every time.
    public static class ProjectPlanEngine
    {
        private static int s_ReactiveUIInitialized;

        public static void Initialize()
        {
            InitializeReactiveUI();

            // Before any view-model exists, because each chart takes its font as
            // its plot is built.
            ChartFonts.Register();
        }

        private static void InitializeReactiveUI()
        {
            // ReactiveUI 23 requires explicit initialization before any WhenAnyValue is used.
            // The desktop app does this via Avalonia's .UseReactiveUI(); a headless host has no
            // UI platform, so initialize the core (non-UI) ReactiveUI services directly. The
            // initialization is process-global, so it must run exactly once.
            if (Interlocked.Exchange(ref s_ReactiveUIInitialized, 1) == 0)
            {
                RxAppBuilder.CreateReactiveUIBuilder()
                    .WithCoreServices()
                    .BuildApp();
            }
        }
    }
}
