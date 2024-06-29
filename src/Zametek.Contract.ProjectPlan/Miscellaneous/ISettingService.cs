using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface ISettingService
    {
        string SettingsFilename { get; }

        string ProjectTitle { get; }

        string ProjectDirectory { get; }

        void SetFilePath(string filename);

        void SetTitle(string filename);

        void SetDirectory(string filename);

        ArrowGraphSettingsModel DefaultArrowGraphSettings { get; }

        ResourceSettingsModel DefaultResourceSettings { get; }

        WorkStreamSettingsModel DefaultWorkStreamSettings { get; }

        //void SetMainViewSettings(MainViewSettingsModel mainViewSettings);

        //MainViewSettingsModel MainViewSettings { get; }

        void Reset();
    }
}
