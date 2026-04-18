using System.ComponentModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedHolidayViewModel
        : IDisposable, INotifyPropertyChanged, IKillSubscriptions, IMuteEdits
    {
        int Id { get; }

        string Name { get; set; }

        string Notes { get; set; }

        RecurrenceRuleModel? RecurrenceRule { get; set; }

        DateTime? StartDateTime { get; set; }

        string RecurrencePattern { get; }

        string RecurrencePatternDisplay { get; }

        bool IsEditing { get; }
    }
}
