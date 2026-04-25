using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISettingService
    {
        string SettingsFilename { get; }

        string LayoutFilename { get; }

        Guid ProjectId { get; }

        string ProjectTitle { get; }

        Guid ScenarioId { get; }

        string ScenarioTitle { get; }

        string ProjectDirectory { get; }

        string Layout { get; set; }

        bool DefaultShowDates { get; set; }

        bool DefaultUseClassicDates { get; set; }

        NonWorkingDayMode DefaultNonWorkingDayMode { get; set; }

        bool DefaultHideCost { get; set; }

        bool DefaultHideBilling { get; set; }






        SortMode ProjectScenarioSortMode { get; set; }

        SortDirection ProjectScenarioSortDirection { get; set; }

        bool ScenarioChartShowNames { get; set; }

        TrackedMetrics ScenarioChartTrackedMetricXAxis { get; set; }

        TrackedMetrics ScenarioChartTrackedMetricYAxis { get; set; }

        CurveFittingType ScenarioChartCurveFittingType { get; set; }





        string SelectedTheme { get; set; }

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
