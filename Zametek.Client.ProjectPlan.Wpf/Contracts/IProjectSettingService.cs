namespace Zametek.Client.ProjectPlan.Wpf
{
    public interface IProjectSettingService
    {
        string PlanTitle
        {
            get;
        }

        string PlanDirectory
        {
            get;
        }

        void SetFilePath(string filename);
        void SetTitle(string filename);
        void SetDirectory(string filename);
        void Reset();
    }
}
