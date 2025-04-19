using System;
using System.IO;

namespace Zametek.ProjectPlan
{
    public class SettingFileHelper
    {
        private const string c_AppData = @"APPDATA";
        private const string c_Home = @"HOME";
        private const string c_Zametek = @"Zametek";
        private const string c_ZametekHome = @".zametek";
        private const string c_Product = @"projectplan.net";
        private const string c_UserSettings = @"UserSettings.json";

        public static string DefaultFileLocation()
        {
            // For backwards compatibility, this checks env vars first before using Env.GetFolderPath/

            // For Windows this should be "C:\Users\<user>\"
            // For Linux/Mac this should be "/home/<user>/"
            string? root = Environment.GetEnvironmentVariable(c_Home)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (string.IsNullOrWhiteSpace(root))
            {
                // For Windows this should be "C:\Users\<user>\AppData\Roaming\"
                // For Linux/Mac this should be "/home/<user>/.config/"
                root = Environment.GetEnvironmentVariable(c_AppData)
                    ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

                if (string.IsNullOrWhiteSpace(root))
                {
                    root = AppContext.BaseDirectory; // This fallback if everything else fails.
                }
                else
                {
                    root = Path.Combine(root, c_Zametek);
                }
            }
            else
            {
                root = Path.Combine(root, c_ZametekHome);
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_UnableToDetermineUserSettingsPath);
            }

            return Path.Combine(root, c_Product, c_UserSettings);
        }
    }
}
