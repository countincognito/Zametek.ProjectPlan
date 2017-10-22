namespace Zametek.Client.ProjectPlan.Wpf
{
    public class AppSettingService
        : IAppSettingService
    {
        public string ProjectPlanFolder
        {
            get
            {
                return AppSettings.ProjectPlanFolder;
            }
            set
            {
                AppSettings.ProjectPlanFolder = value;
            }
        }
    }
}
