using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    /// <summary>
    /// A walk through the preset colors, from which a build draws the colors of the series that have none of their own.
    /// A build keeps a sequence of its own, so that no build shifts the colors of another.
    /// </summary>
    public interface IPresetColorSequence
    {
        /// <summary>
        /// The next color of the sequence. After the last color, the sequence starts again from the first.
        /// </summary>
        ColorFormatModel Next();

        /// <summary>
        /// Starts the sequence again from its first color.
        /// </summary>
        void Reset();
    }
}
