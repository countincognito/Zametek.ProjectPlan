using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Zametek.Contract.ProjectPlan
{
    public interface IHolidaySettingsManagerViewModel
    {
        bool IsBusy { get; }

        bool HasStaleOutputs { get; }

        bool HasCompilationErrors { get; }

        bool HasSelectedHoliday { get; }

        bool HasSelectedHolidays { get; }

        bool AreSettingsUpdated { get; set; }

        IReadOnlyList<IManagedHolidayViewModel> RawHolidays { get; }

        ReadOnlyObservableCollection<IManagedHolidayViewModel> Holidays { get; }

        ICommand SetSelectedManagedHolidaysCommand { get; }

        ICommand AddManagedHolidayCommand { get; }

        ICommand RemoveManagedHolidaysCommand { get; }

        ICommand DuplicateManagedHolidayCommand { get; }

        ICommand EditManagedHolidayCommand { get; }
    }
}
