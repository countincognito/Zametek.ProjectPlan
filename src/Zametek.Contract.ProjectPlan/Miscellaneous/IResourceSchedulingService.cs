using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IResourceSchedulingService
    {
        /// <summary>
        /// Builds the series of the resources. A resource with no color of its own takes the next color of
        /// <paramref name="presetColors"/>, and the series start that sequence again from its first color.
        /// </summary>
        ResourceSeriesSetModel BuildResourceSeriesSet(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            ResourceSettingsModel resourceSettings,
            IPresetColorSequence presetColors);

        /// <summary>
        /// Builds the series of the tracking. A resource with no color of its own takes the next color of
        /// <paramref name="presetColors"/>. Give it the sequence that the series of the resources used, and it carries
        /// on where they stopped.
        /// </summary>
        TrackingSeriesSetModel BuildTrackingSeriesSet(
            IEnumerable<ActivityModel> activities,
            ResourceSettingsModel resourceSettings,
            bool hasResources,
            IPresetColorSequence presetColors);

        TrackingSeriesSetModel CombineResourceTrackingSeries(
            TrackingSeriesSetModel trackingSeriesSet,
            IEnumerable<int> resourceIds);
    }
}
