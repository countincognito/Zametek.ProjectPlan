using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISettingService
    {
        string SettingsFilename { get; }

        string ProjectTitle { get; }

        string ProjectDirectory { get; }

        bool ShowDates { get; set; }

        bool UseClassicDates { get; set; }

        bool UseBusinessDays { get; set; }

        string SelectedTheme { get; set; }

        bool IsTitleBoundToFilename { get; set; }

        void SetProjectFilePath(string filename, bool bindTitleToFilename);

        void SetProjectTitle(string filename);

        void SetProjectDirectory(string filename);

        ArrowGraphSettingsModel DefaultArrowGraphSettings { get; }

        ResourceSettingsModel DefaultResourceSettings { get; }

        WorkStreamSettingsModel DefaultWorkStreamSettings { get; }

        void Reset();
    }
}
