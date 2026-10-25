using Avalonia.Media;
using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    // A walk through the preset colours, from the first, wrapping round at the end. Each build that draws
    // colours keeps a sequence of its own: one shared between builds would let each shift the colours of the
    // other, and two jobs running at once would draw from it in whatever order their threads happened to run.
    public sealed class PresetColorSequence
    {
        private int m_Drawn;

        public ColorFormatModel Next()
        {
            // The count is taken atomically and wrapped as an unsigned number, so the index stays in range
            // however many threads draw at once.
            uint drawn = unchecked((uint)Interlocked.Increment(ref m_Drawn) - 1u);
            Color color = ColorHelper.PresetColors[(int)(drawn % (uint)ColorHelper.PresetColors.Count)];
            return ColorHelper.AvaloniaColorToColorFormatModel(color);
        }

        public void Reset()
        {
            Interlocked.Exchange(ref m_Drawn, 0);
        }
    }
}
