using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISettingService
    {
        string SettingsFilename { get; }

        Guid ProjectId { get; }

        string ProjectTitle { get; }

        Guid ProjectPlanId { get; }

        string ProjectPlanTitle { get; }

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

        void SetProjectId(Guid projectId);

        void SetProjectDirectory(string filename);

        void SetProjectPlanTitle(string name);

        void SetProjectPlanId(Guid planId);

        GraphSettingsModel DefaultGraphSettings { get; }

        ResourceSettingsModel DefaultResourceSettings { get; }

        WorkStreamSettingsModel DefaultWorkStreamSettings { get; }

        void ResetProject();

        void ResetProjectPlan();
    }
}
