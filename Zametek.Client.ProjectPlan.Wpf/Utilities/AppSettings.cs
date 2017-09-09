using System;
using Zametek.Client.ProjectPlan.Wpf.Properties;

namespace Zametek.Client.ProjectPlan.Wpf.Utilities
{
    public static class AppSettings
    {
        public static string LastUsedFolder
        {
            get
            {
                return string.IsNullOrWhiteSpace(Settings.Default.LastUsedFolder)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : Settings.Default.LastUsedFolder;
            }
            set
            {
                Settings.Default.LastUsedFolder = value;
                Settings.Default.Save();
            }
        }
    }
}
