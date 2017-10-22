using System;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public static class AppSettings
    {
        public static string ProjectPlanFolder
        {
            get
            {
                return string.IsNullOrWhiteSpace(Properties.Settings.Default.ProjectPlanFolder)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : Properties.Settings.Default.ProjectPlanFolder;
            }
            set
            {
                Properties.Settings.Default.ProjectPlanFolder = value;
                Properties.Settings.Default.Save();
            }
        }
    }
}
