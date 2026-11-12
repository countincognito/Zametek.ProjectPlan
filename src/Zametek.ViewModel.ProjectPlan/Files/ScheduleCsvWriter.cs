using Zametek.Common.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Writes the data of a compiled scenario as CSV, as <see cref="SimulationCsvWriter"/> writes the files of a simulation:
    /// the table of the critical path method, the series of the resources, and the series of the earned value. Each time is a
    /// number of days from the start of the project. A list of ids is written with a space between two ids.
    /// </summary>
    public static class ScheduleCsvWriter
    {
        /// <summary>
        /// One row for each activity, in the order of the ids: the duration, the earliest and the latest start and finish, the
        /// total and the free slack, the dependencies, the dependencies that the resources add, the resources that do the
        /// activity, and the minimum earliest start and the maximum latest finish. A value that the activity does not have is
        /// empty.
        /// </summary>
        public static void WriteSchedule(
            TextWriter writer,
            IEnumerable<DependentActivityModel> dependentActivities)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(dependentActivities);

            CsvText.WriteRow(
                writer,
                @"Id",
                @"Name",
                @"Duration",
                @"EarliestStartTime",
                @"EarliestFinishTime",
                @"LatestStartTime",
                @"LatestFinishTime",
                @"TotalSlack",
                @"FreeSlack",
                @"Dependencies",
                @"ResourceDependencies",
                @"AllocatedToResources",
                @"MinimumEarliestStartTime",
                @"MaximumLatestFinishTime");

            foreach (DependentActivityModel dependentActivity in dependentActivities.OrderBy(static x => x.Activity.Id))
            {
                ActivityModel activity = dependentActivity.Activity;

                CsvText.WriteRow(
                    writer,
                    CsvText.Number(activity.Id),
                    activity.Name,
                    CsvText.Number(activity.Duration),
                    CsvText.Number(activity.EarliestStartTime),
                    CsvText.Number(activity.EarliestFinishTime),
                    CsvText.Number(activity.LatestStartTime),
                    CsvText.Number(activity.LatestFinishTime),
                    CsvText.Number(activity.TotalSlack),
                    CsvText.Number(activity.FreeSlack),
                    Ids(dependentActivity.Dependencies),
                    Ids(dependentActivity.ResourceDependencies),
                    Ids(activity.AllocatedToResources),
                    CsvText.Number(activity.MinimumEarliestStartTime),
                    CsvText.Number(activity.MaximumLatestFinishTime));
            }
        }

        /// <summary>
        /// One row for each day and each resource, in the order of the days, and then of the display order of the resources, as
        /// the sheets of the resource chart have them: whether the resource is allocated to the project, works on an activity
        /// and has effort on the day, and its cost on the day. The days are those of the longest series. The series of the
        /// activities that no resource of the plan does has no id.
        /// </summary>
        public static void WriteResourceSeries(
            TextWriter writer,
            ResourceSeriesSetModel resourceSeriesSet)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);

            CsvText.WriteRow(writer, @"Time", @"ResourceId", @"Resource", @"Allocated", @"Activity", @"Effort", @"Cost");

            List<ResourceSeriesModel> series = [.. resourceSeriesSet.Combined.OrderBy(static x => x.DisplayOrder)];
            int days = series.Select(static x => x.ResourceSchedule.ResourceAllocation.Count).DefaultIfEmpty().Max();

            for (int day = 0; day < days; day++)
            {
                foreach (ResourceSeriesModel resourceSeries in series)
                {
                    ResourceScheduleModel schedule = resourceSeries.ResourceSchedule;
                    int resourceId = schedule.Resource.Id;

                    CsvText.WriteRow(
                        writer,
                        CsvText.Number(day),
                        resourceId == default ? string.Empty : CsvText.Number(resourceId),
                        resourceSeries.Title,
                        CsvText.Boolean(IsSet(schedule.ResourceAllocation, day)),
                        CsvText.Boolean(IsSet(schedule.ActivityAllocation, day)),
                        CsvText.Boolean(IsSet(schedule.EffortAllocation, day)),
                        CsvText.Number(IsSet(schedule.CostAllocation, day) ? resourceSeries.UnitCost : 0.0));
                }
            }
        }

        /// <summary>
        /// The points of the planned earned value, and then of the effort and of the progress that the trackers record, as the
        /// sheets of the earned value chart have them, with the name of the series in the first column.
        /// </summary>
        public static void WriteEarnedValueSeries(
            TextWriter writer,
            TrackingSeriesSetModel trackingSeriesSet)
        {
            ArgumentNullException.ThrowIfNull(writer);
            ArgumentNullException.ThrowIfNull(trackingSeriesSet);

            CsvText.WriteRow(writer, @"Series", @"Time", @"ActivityId", @"ActivityName", @"Value", @"ValuePercentage");

            foreach ((string name, List<TrackingPointModel> points) in new[]
            {
                (@"Plan", trackingSeriesSet.Plan),
                (@"Effort", trackingSeriesSet.Effort),
                (@"Progress", trackingSeriesSet.Progress),
            })
            {
                foreach (TrackingPointModel point in points)
                {
                    CsvText.WriteRow(
                        writer,
                        name,
                        CsvText.Number(point.Time),
                        CsvText.Number(point.ActivityId),
                        point.ActivityName,
                        CsvText.Number(point.Value),
                        CsvText.Number(point.ValuePercentage));
                }
            }
        }

        private static bool IsSet(List<bool> allocation, int day) => day < allocation.Count && allocation[day];

        private static string Ids(IEnumerable<int> ids) => string.Join(' ', ids.Order().Select(CsvText.Number));
    }
}
