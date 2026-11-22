using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// The target of a simulation as a finish time. The settings give the target as a number of working days
    /// (<see cref="SimulationSettingsModel.TargetFinishTime"/>), as a date
    /// (<see cref="SimulationSettingsModel.TargetFinishDateTime"/>), or as both. The date takes the place of the number, as
    /// the constraint dates of an activity do.
    /// </summary>
    /// <remarks>
    /// People give a deadline as a date, and "done by June 30" means "done by the end of June 30". So the target date is the
    /// last day on which work can be done. Its finish time is the number of working days from the start of the project up
    /// to and including the date. A maximum latest finish date of an activity is the first day after the last day of work,
    /// and so the target date gives the finish time that a maximum latest finish date of the next day gives. A date that
    /// is not a working day gives the time of the working day before it, and a date before the start gives day 0.
    /// </remarks>
    public static class SimulationTargetHelper
    {
        /// <summary>
        /// The finish time of a project that is done by the end of the date, in the calendar of the calculator.
        /// </summary>
        public static int FinishTimeByEndOf(
            IDateTimeCalculator dateTimeCalculator,
            DateTimeOffset projectStart,
            DateTimeOffset date)
        {
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            (int? time, _) = dateTimeCalculator.CalculateConstraintTimeAndDateTime(projectStart, date.AddDays(1));
            return time ?? 0;
        }

        /// <summary>
        /// The target that a run uses, or null when the settings have none.
        /// </summary>
        public static int? Resolve(
            SimulationSettingsModel settings,
            IDateTimeCalculator dateTimeCalculator,
            DateTimeOffset projectStart)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return settings.TargetFinishDateTime is DateTimeOffset date
                ? FinishTimeByEndOf(dateTimeCalculator, projectStart, date)
                : settings.TargetFinishTime;
        }

        /// <summary>
        /// The settings with the number that their target date gives. Settings with no target date come back as they are.
        /// A file gets these, so that a program that reads only the number reads the target of the date.
        /// </summary>
        public static SimulationSettingsModel Normalize(
            SimulationSettingsModel settings,
            IDateTimeCalculator dateTimeCalculator,
            DateTimeOffset projectStart)
        {
            ArgumentNullException.ThrowIfNull(settings);

            if (settings.TargetFinishDateTime is null)
            {
                return settings;
            }

            int? target = Resolve(settings, dateTimeCalculator, projectStart);
            return settings.TargetFinishTime == target ? settings : settings with { TargetFinishTime = target };
        }
    }
}
