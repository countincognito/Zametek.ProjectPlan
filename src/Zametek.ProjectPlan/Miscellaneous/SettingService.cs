using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Zametek.Common.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public class SettingService
        : SettingServiceBase
    {
        #region Fields

        private readonly Lock m_Lock;
        private string m_DockLayout;
        private readonly Dictionary<string, DataGridModel> m_DataGridLayout;

        #endregion

        #region Ctors

        public SettingService(
            string settingsFilename,
            string dockLayoutFilename,
            string dataGridLayoutFilename)
            : base(settingsFilename)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilename);
            ArgumentException.ThrowIfNullOrWhiteSpace(dockLayoutFilename);
            ArgumentException.ThrowIfNullOrWhiteSpace(dataGridLayoutFilename);
            DockLayoutFilename = dockLayoutFilename;
            DataGridLayoutFilename = dataGridLayoutFilename;
            m_Lock = new();
            m_DockLayout = string.Empty;
            m_DataGridLayout = [];
            string? directory = Path.GetDirectoryName(SettingsFilename);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_UnableToDetermineUserSettingsDirectory);
            }

            Directory.CreateDirectory(directory);

            if (File.Exists(DockLayoutFilename))
            {
                using StreamReader reader = File.OpenText(DockLayoutFilename);
                string content = reader.ReadToEnd();
                m_DockLayout = content;
            }

            if (File.Exists(DataGridLayoutFilename))
            {
                using StreamReader reader = File.OpenText(DataGridLayoutFilename);
                string content = reader.ReadToEnd();

                try
                {
                    List<DataGridModel>? dataGridModels = JsonConvert.DeserializeObject<List<DataGridModel>>(content);
                    if (dataGridModels is not null)
                    {
                        foreach (DataGridModel model in dataGridModels)
                        {
                            m_DataGridLayout[model.Name] = model;
                        }
                    }
                }
                catch (Exception)
                {
                    m_DataGridLayout.Clear();
                }
            }
        }

        #endregion

        private void SaveDockLayout()
        {
            lock (m_Lock)
            {
                using StreamWriter writer = File.CreateText(DockLayoutFilename);
                writer.WriteLine(DockLayout);
            }
        }

        private void SaveDataGridLayout()
        {
            lock (m_Lock)
            {
                using StreamWriter writer = File.CreateText(DataGridLayoutFilename);
                writer.WriteLine(JsonConvert.SerializeObject(m_DataGridLayout.Values, Formatting.Indented));
            }
        }

        private void SaveSettings()
        {
            lock (m_Lock)
            {
                using StreamWriter writer = File.CreateText(SettingsFilename);
                var jsonSerializer = JsonSerializer.Create(
                    new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Ignore,
                    });
                Data.ProjectPlan.v0_6_0.AppSettingsModel output = Converter.Format(m_AppSettingsModel);
                jsonSerializer.Serialize(writer, output, output.GetType());
            }
        }

        #region ISettingService Members

        public override string ProjectDirectory
        {
            get
            {
                string directory = m_AppSettingsModel.ProjectDirectory;
                return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : directory;
            }
            protected set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { ProjectDirectory = value };
                    SaveSettings();
                }
            }
        }

        public override string DockLayout
        {
            get
            {
                return m_DockLayout;
            }
            set
            {
                lock (m_Lock)
                {
                    m_DockLayout = value;
                    SaveDockLayout();
                }
            }
        }

        public override DataGridModel GetDataGridLayout(string name)
        {
            if (m_DataGridLayout.TryGetValue(name, out DataGridModel? model))
            {
                return model;
            }
            else
            {
                return new DataGridModel
                {
                    Name = name
                };
            }
        }

        public override void SetDataGridLayout(
            string name,
            DataGridModel model)
        {
            m_DataGridLayout[name] = model;
            SaveDataGridLayout();
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

        public override NonWorkingDayMode DefaultNonWorkingDayMode
        {
            get
            {
                return m_AppSettingsModel.DefaultNonWorkingDayMode;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { DefaultNonWorkingDayMode = value };
                    SaveSettings();
                }
            }
        }

        public override bool DefaultHideCost
        {
            get
            {
                return m_AppSettingsModel.DefaultHideCost;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { DefaultHideCost = value };
                    SaveSettings();
                }
            }
        }

        public override bool DefaultHideBilling
        {
            get
            {
                return m_AppSettingsModel.DefaultHideBilling;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { DefaultHideBilling = value };
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
