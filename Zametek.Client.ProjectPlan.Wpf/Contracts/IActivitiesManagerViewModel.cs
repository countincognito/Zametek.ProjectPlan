using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IActivitiesManagerViewModel
    {
        bool ShowDates
        {
            get;
            set;
        }

        bool ShowDays
        {
            get;
        }

        ICommand AddManagedActivityCommand
        {
            get;
        }

        ICommand RemoveManagedActivityCommand
        {
            get;
        }
    }
}
