using System;
using System.IO;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class ProjectSettingService
        : IProjectSettingService
    {
        private string m_PlanTitle;

        public string PlanTitle
        {
            get
            {
                return string.IsNullOrWhiteSpace(m_PlanTitle) ? null : m_PlanTitle;
            }
            private set
            {
                m_PlanTitle = value;
            }
        }

        public string PlanDirectory
        {
            get
            {
                string directory = Properties.Settings.Default.ProjectPlanDirectory;
                return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : directory;
            }
            private set
            {
                Properties.Settings.Default.ProjectPlanDirectory = value;
                Properties.Settings.Default.Save();
            }
        }

        public void SetFilePath(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }
            SetTitle(filename);
            SetDirectory(filename);
        }

        public void SetTitle(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }
            PlanTitle = Path.GetFileNameWithoutExtension(filename);
        }

        public void SetDirectory(string filename)
        {
            if (string.IsNullOrWhiteSpace(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }
            PlanDirectory = Path.GetDirectoryName(filename);
        }

        public void Reset()
        {
            PlanTitle = null;
        }
    }
}
