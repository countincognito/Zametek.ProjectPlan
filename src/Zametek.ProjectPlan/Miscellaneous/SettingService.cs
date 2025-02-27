using Newtonsoft.Json;
using System;
using System.IO;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public class SettingService
        : SettingServiceBase
    {
        #region Fields

        private readonly object m_Lock;

        #endregion

        #region Ctors

        public SettingService(string settingsFilename)
            : base(settingsFilename)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilename);
            m_Lock = new object();
            string? directory = Path.GetDirectoryName(SettingsFilename);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_UnableToDetermineUserSecretsPath);
            }

            Directory.CreateDirectory(directory);
        }

        #endregion

        private void SaveSettings()
        {
            using StreamWriter writer = File.CreateText(SettingsFilename);
            var jsonSerializer = JsonSerializer.Create(
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                });
            jsonSerializer.Serialize(writer, m_AppSettingsModel, m_AppSettingsModel.GetType());
        }

        #region ISettingService Members

        public override string ProjectDirectory
        {
            get
            {
                string directory = m_AppSettingsModel.ProjectPlanDirectory;
                return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : directory;
            }
            protected set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { ProjectPlanDirectory = value };
                    SaveSettings();
                }
            }
        }

        public override bool DefaultShowDates
        {
            get
            {
                return m_AppSettingsModel.DefaultShowDates;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { DefaultShowDates = value };
                    SaveSettings();
                }
            }
        }

        public override bool DefaultUseClassicDates
        {
            get
            {
                return m_AppSettingsModel.DefaultUseClassicDates;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { DefaultUseClassicDates = value };
                    SaveSettings();
                }
            }
        }

        public override bool DefaultUseBusinessDays
        {
            get
            {
                return m_AppSettingsModel.DefaultUseBusinessDays;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { DefaultUseBusinessDays = value };
                    SaveSettings();
                }
            }
        }

        public override string SelectedTheme
        {
            get
            {
                return m_AppSettingsModel.SelectedTheme;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { SelectedTheme = value };
                    SaveSettings();
                }
            }
        }

        #endregion
    }
}
