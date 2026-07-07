using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using System;

namespace Zametek.View.ProjectPlan
{
    // Fades a control in on activation. Applied centrally (see ViewLocator.Build) to every dock tab view,
    // so switching tabs gives a consistent quick fade-in rather than an instant swap. The view is hidden
    // the moment the behavior attaches - during ViewLocator.Build, before the view's first paint - and
    // revealed with a short opacity transition once it has loaded. Grids additionally coordinate their
    // scroll restore under this fade via DataGridPersistScrollBehavior.
    //
    // SCOTTPLOT CAVEAT: a ScottPlot chart (AvaPlot) draws via a Skia custom draw operation that leases the
    // canvas and paints at full alpha, so Avalonia's per-node opacity never reaches it - the chart would
    // "pop" in at full opacity while the rest of its tab fades. To make it honour the fade, a chart tab's
    // subtree is forced through a real offscreen compositing layer (an opaque OpacityMask), which the custom
    // op DOES render into; that layer is then composited with the fading Opacity, so the chart fades too.
    // The mask is opaque, so it changes nothing visually, and it is cleared once the fade completes so the
    // chart's normal (direct, faster) rendering resumes for interaction. Only ScottPlot tabs pay this cost;
    // grids and interactive graphs are ordinary controls that already honour opacity.
    public class FadeInBehavior
        : Behavior<Control>
    {
        // An opaque mask: forces the subtree offscreen so custom-drawn (ScottPlot) content is captured in
        // the opacity layer, without altering the pixels.
        private static readonly IBrush s_ForceCompositeLayer = new ImmutableSolidColorBrush(Colors.White);

        private Control? m_Control;
        private bool m_ForcedLayer;
        private IDisposable? m_ClearLayerTimer;

        protected override void OnAttached()
        {
            base.OnAttached();
            m_Control = AssociatedObject;
            if (m_Control is null)
            {
                return;
            }

            // Hide now (before the view is shown) so the swap-in is never seen at full opacity; the
            // reveal below fades it back.
            ViewFade.Hide(m_Control);

            // A ScottPlot chart ignores per-node opacity (see class comment), so force its tab through an
            // offscreen layer for the duration of the fade.
            if (m_Control is ScottPlotUserControl)
            {
                m_Control.OpacityMask = s_ForceCompositeLayer;
                m_ForcedLayer = true;
            }

            m_Control.Loaded += OnLoaded;
        }

        protected override void OnDetaching()
        {
            m_ClearLayerTimer?.Dispose();
            m_ClearLayerTimer = null;
            if (m_Control is not null)
            {
                m_Control.Loaded -= OnLoaded;
                ClearForcedLayer();
                // Never leave a detached view hidden.
                ViewFade.Reveal(m_Control);
            }
            base.OnDetaching();
        }

        private void OnLoaded(
            object? sender,
            RoutedEventArgs e)
        {
            if (m_Control is not Control control)
            {
                return;
            }

            // Reveal on the next tick so the hidden (Opacity 0) state is applied first; the transition
            // installed on attach then animates the 0 -> 1 change into a quick fade.
            Dispatcher.UIThread.Post(
                () =>
                {
                    ViewFade.Reveal(control);

                    // Drop the forced offscreen layer once the fade has finished, so an interactive chart
                    // renders directly (and quickly) again. A small buffer past the fade avoids clearing
                    // mid-animation.
                    if (m_ForcedLayer)
                    {
                        m_ClearLayerTimer = DispatcherTimer.RunOnce(
                            ClearForcedLayer,
                            ViewFade.Duration + TimeSpan.FromMilliseconds(50.0),
                            DispatcherPriority.Background);
                    }
                },
                DispatcherPriority.Background);
        }

        // Remove the opaque layer-forcing mask (restoring the chart's direct rendering). Idempotent.
        private void ClearForcedLayer()
        {
            m_ClearLayerTimer = null;
            if (m_ForcedLayer
                && m_Control is not null)
            {
                m_Control.OpacityMask = null;
            }
            m_ForcedLayer = false;
        }
    }
}
