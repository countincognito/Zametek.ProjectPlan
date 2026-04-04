using System.ComponentModel;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedHolidayViewModel
        : IDisposable, INotifyPropertyChanged, IKillSubscriptions, IMuteEdits
    {
        int Id { get; }

        string Name { get; set; }

        string Notes { get; set; }

        string RecurrencePattern { get; set; }

        string RecurrencePatternDisplay { get; }

        bool IsEditing { get; }
    }
}
