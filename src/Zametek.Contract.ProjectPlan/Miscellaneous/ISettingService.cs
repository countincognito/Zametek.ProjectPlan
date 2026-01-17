using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISettingService
    {
        string SettingsFilename { get; }

        string ProjectTitle { get; }

        string PlanTitle { get; }

        string ProjectDirectory { get; }

        bool DefaultShowDates { get; set; }

        bool DefaultUseClassicDates { get; set; }

        bool DefaultUseBusinessDays { get; set; }

        bool DefaultHideCost { get; set; }

        bool DefaultHideBilling { get; set; }

        string SelectedTheme { get; set; }

        bool IsTitleBoundToFilename { get; set; }

        void SetProjectFilePath(string filename, bool bindTitleToFilename);

        void SetProjectTitle(string filename);

        void SetProjectDirectory(string filename);

        void SetPlanTitle(string name);

        GraphSettingsModel DefaultGraphSettings { get; }

        ResourceSettingsModel DefaultResourceSettings { get; }

        WorkStreamSettingsModel DefaultWorkStreamSettings { get; }

        void Reset();
    }
}
