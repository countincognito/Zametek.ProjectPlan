using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using System;

namespace Zametek.View.ProjectPlan
{
    // Shared quick fade-in used for a consistent tab-activation effect: FadeInBehavior fades every dock
    // tab view in (see ViewLocator), and DataGridPersistScrollBehavior fades a grid back in after its
    // scroll restore. Hiding is instant - the opacity transition is installed only AFTER Opacity is set
    // to 0 - so the hide never animates (which would itself flash the pre-scroll content); only the
    // reveal animates.
    internal static class ViewFade
    {
        public static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(250.0);

        // Hide the control instantly, then install the opacity transition so a later Reveal fades.
        public static void Hide(Control control)
        {
            control.Opacity = 0.0;
            EnsureTransition(control);
        }

        // Fade the control back to fully opaque (animated by the transition installed in Hide).
        public static void Reveal(Control control)
        {
            control.Opacity = 1.0;
        }

        private static void EnsureTransition(Control control)
        {
            control.Transitions ??= [];

            // Do not stack duplicate opacity transitions if a control is hidden more than once.
            foreach (ITransition transition in control.Transitions)
            {
                if (transition is DoubleTransition existing
                    && existing.Property == Visual.OpacityProperty)
                {
                    return;
                }
            }

            control.Transitions.Add(new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = Duration,
                Easing = new CubicEaseOut(),
            });
        }
    }
}
