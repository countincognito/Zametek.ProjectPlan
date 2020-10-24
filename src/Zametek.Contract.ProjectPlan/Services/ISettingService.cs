using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISettingService
    {
        string PlanTitle { get; }

        string PlanDirectory { get; }

        void SetFilePath(string filename);

        void SetTitle(string filename);

        void SetDirectory(string filename);

        ArrowGraphSettingsModel DefaultArrowGraphSettings { get; }

        ResourceSettingsModel DefaultResourceSettings { get; }

        void SetMainViewSettings(MainViewSettingsModel mainViewSettings);
        
        MainViewSettingsModel MainViewSettings { get; }

        void Reset();
    }
}
