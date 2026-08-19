using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISettingService
    {
        string SettingsFilename { get; }

        string DockLayoutFilename { get; }

        string DataGridLayoutFilename { get; }

        Guid ProjectId { get; }

        string ProjectTitle { get; }

        Guid ScenarioId { get; }

        string ScenarioTitle { get; }

        string ProjectDirectory { get; }

        string DockLayout { get; set; }

        IList<DataGridModel> GetDataGridLayout();

        void SetDataGridLayout(IList<DataGridModel> models);

        bool DefaultShowDates { get; set; }

        bool DefaultUseClassicDates { get; set; }

        NonWorkingDayMode DefaultNonWorkingDayMode { get; set; }

        bool DefaultHideCost { get; set; }

        bool DefaultHideBilling { get; set; }

        string SelectedTheme { get; set; }

        int MaxRecentProjectFilePaths { get; }

        // The time budget for a single graph compilation, in milliseconds. Zero or
        // less means no limit. Exceeding it cancels the compilation and raises a
        // GraphCompilationTimeoutException.
        int CompilationTimeoutMilliseconds { get; set; }

        IReadOnlyList<string> RecentProjectFilePaths { get; }

        void RecordRecentProjectFilePath(string filename);

        void RemoveRecentProjectFilePath(string filename);

        void ClearRecentProjectFilePaths();

        bool IsTitleBoundToFilename { get; set; }

        void SetProjectFilePath(string filename, bool bindTitleToFilename);

        void SetProjectTitle(string filename);

        void SetProjectId(Guid projectId);

        void SetProjectDirectory(string filename);

        void SetProjectScenarioTitle(string name);

        void SetProjectScenarioId(Guid scenarioId);

        GraphSettingsModel DefaultGraphSettings { get; }

        ResourceSettingsModel DefaultResourceSettings { get; }

        WorkStreamSettingsModel DefaultWorkStreamSettings { get; }

        HolidaySettingsModel DefaultHolidaySettings { get; }

        void ResetProject();

        void ResetProjectScenario();
    }
}
