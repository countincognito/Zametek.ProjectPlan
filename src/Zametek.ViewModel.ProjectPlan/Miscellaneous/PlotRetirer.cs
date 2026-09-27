using ScottPlot;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Disposes each ScottPlot <see cref="Plot"/> that a chart manager view model replaces
    /// during a rebuild. Plot is IDisposable (it owns unmanaged SkiaSharp resources invisible
    /// to the GC), but the rebuild-and-swap pattern used by the chart managers abandons the
    /// outgoing plot, so without this the unmanaged memory only returns via finalizers and
    /// the working set grows with every rebuild.
    /// </summary>
    /// <remarks>
    /// Disposal is deliberately indirect, because the outgoing plot can still have two kinds of
    /// consumer at the moment it is swapped out:
    /// <para>
    /// - The UI: the swap raises a property change that data binding processes on the UI thread,
    ///   which may be later than the swap itself (the chart rebuilds run on the taskpool). The
    ///   view only re-hosts the new plot when that binding lands, so disposal is deferred through
    ///   the dispatcher, which on the UI thread runs it only after pending binding updates and
    ///   render passes have finished with the old plot. Headless nothing is bound to the plot and
    ///   nothing renders it, so the dispatcher there disposes it where it stands.
    /// </para>
    /// <para>
    /// - Image exports: RenderChartImageAsync and the save-image commands snapshot the current
    ///   Plot and render it on the taskpool, so a dispatcher post alone could still dispose it
    ///   mid-render. Retirement is therefore one generation deep: the newly outgoing plot is only
    ///   held, and the plot retired on the previous rebuild is the one disposed. An export would
    ///   have to span two whole rebuilds before it could observe a disposed plot.
    /// </para>
    /// The cost is one retained plot per chart, reclaimed on the next rebuild or when the owning
    /// view model is disposed.
    /// </remarks>
    public sealed class PlotRetirer
        : IDisposable
    {
        private readonly Lock m_Lock;
        private readonly IUIDispatcher m_UIDispatcher;
        private Plot? m_Retired;
        private bool m_Disposed;

        public PlotRetirer(IUIDispatcher uiDispatcher)
        {
            ArgumentNullException.ThrowIfNull(uiDispatcher);
            m_Lock = new();
            m_UIDispatcher = uiDispatcher;
        }

        public void Retire(Plot outgoing)
        {
            ArgumentNullException.ThrowIfNull(outgoing);
            Plot? previous;

            lock (m_Lock)
            {
                if (m_Disposed)
                {
                    // The owning view model is already disposed; nothing can still be
                    // showing or exporting the outgoing plot, so dispose it directly.
                    outgoing.Dispose();
                    return;
                }
                previous = m_Retired;
                m_Retired = outgoing;
            }

            if (previous is not null)
            {
                // Deferring runs this after data binding and rendering have moved the UI onto the
                // newer plots, so the disposed plot can no longer be drawn.
                m_UIDispatcher.Defer(previous.Dispose);
            }
        }

        public void Dispose()
        {
            Plot? retired;

            lock (m_Lock)
            {
                if (m_Disposed)
                {
                    return;
                }
                m_Disposed = true;
                retired = m_Retired;
                m_Retired = null;
            }

            // Synchronous disposal: this runs at owner tear-down (application or CLI exit),
            // when a dispatcher post might never be serviced.
            retired?.Dispose();
        }
    }
}
