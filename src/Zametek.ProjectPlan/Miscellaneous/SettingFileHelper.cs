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
        private const string c_DockLayout = @"DockLayout.json";
        private const string c_DataGridLayout = @"DataGridLayout.json";

        public static string? UserProfileFolderLocation()
        {
            // For Windows this should be "C:\Users\<user>\"
            // For Linux/Mac this should be "/home/<user>/"
            return Environment.GetEnvironmentVariable(c_Home)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        public static string? ZametekUserProfileFolderLocation()
        {
            string? root = UserProfileFolderLocation();

            if (!string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(root, c_ZametekHome);
            }

            return root;
        }

        public static string? AppDataFolderLocation()
        {
            // For Windows this should be "C:\Users\<user>\AppData\Roaming\"
            // For Linux/Mac this should be "/home/<user>/.config/"
            return Environment.GetEnvironmentVariable(c_AppData)
                ?? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        public static string ZametekAppDataFolderLocation()
        {
            string? root = AppDataFolderLocation();

            if (string.IsNullOrWhiteSpace(root))
            {
                root = AppContext.BaseDirectory; // This fallback if everything else fails.
            }
            else
            {
                root = Path.Combine(root, c_Zametek);
            }

            return root;
        }

        public static string ProductSettingsFolderLocation()
        {
            // For backwards compatibility, this checks env vars first before using Env.GetFolderPath/

            string? root = ZametekUserProfileFolderLocation();

            if (string.IsNullOrWhiteSpace(root))
            {
                root = ZametekAppDataFolderLocation();
            }

            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_UnableToDetermineProductSettingsFolderPath);
            }

            return Path.Combine(root, c_Product);
        }

        public static string DefaultUserSettingsFileLocation()
        {
            string productFolderPath = ProductSettingsFolderLocation();
            return Path.Combine(productFolderPath, c_UserSettings);
        }

        public static string DefaultDockLayoutFileLocation()
        {
            string productFolderPath = ProductSettingsFolderLocation();
            return Path.Combine(productFolderPath, c_DockLayout);
        }

        public static string DefaultDataGridLayoutFileLocation()
        {
            string productFolderPath = ProductSettingsFolderLocation();
            return Path.Combine(productFolderPath, c_DataGridLayout);
        }
    }
}
