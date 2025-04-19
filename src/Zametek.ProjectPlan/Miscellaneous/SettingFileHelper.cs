using System;
using System.IO;
using System.Reflection;

namespace Zametek.ProjectPlan
{
    public class SettingFileHelper
    {
        private const string c_AppData = @"APPDATA";
        private const string c_Home = @"HOME";
        private const string c_Zametek = @"Zametek";
        private const string c_Product = @"projectplan.net";
        private const string c_UserSettings = @"UserSettings.json";

        public static string DefaultFileLocation()
        {
            // For backwards compatibility, this checks env vars first before using Env.GetFolderPath/
            string? appData = Environment.GetEnvironmentVariable(c_AppData);
            string? root = appData
                ?? Environment.GetEnvironmentVariable(c_Home)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.Personal)
                ?? Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location); // This fallback if everything else fails.

            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_UnableToDetermineUserSettingsPath);
            }

            return Path.Combine(root, c_Zametek, c_Product, c_UserSettings);
        }
    }
}
