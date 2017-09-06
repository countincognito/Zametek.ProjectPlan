using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IMainViewModel
    {
        string ProjectTitle
        {
            get;
        }

        bool IsProjectUpdated
        {
            get;
        }

        DateTime ProjectStart
        {
            get;
            set;
        }

        bool ShowDates
        {
            get;
            set;
        }

        bool UseBusinessDays
        {
            get;
            set;
        }

        bool AutoCompile
        {
            get;
            set;
        }

        double? DirectCost
        {
            get;
        }

        double? IndirectCost
        {
            get;
        }

        double? OtherCost
        {
            get;
        }

        double? TotalCost
        {
            get;
        }

        ICommand OpenProjectPlanFileCommand
        {
            get;
        }

        ICommand SaveProjectPlanFileCommand
        {
            get;
        }

        ICommand ImportMicrosoftProjectCommand
        {
            get;
        }

        ICommand CloseProjectCommand
        {
            get;
        }

        ICommand OpenResourceSettingsCommand
        {
            get;
        }

        ICommand OpenArrowGraphSettingsCommand
        {
            get;
        }

        ICommand CompileCommand
        {
            get;
        }

        Task DoOpenProjectPlanFileAsync(string fileName = null);
        void ResetProject();
    }
}
