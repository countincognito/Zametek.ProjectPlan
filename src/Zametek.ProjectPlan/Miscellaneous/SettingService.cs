using Newtonsoft.Json;
using ReactiveUI;
using System;
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

        #endregion

        #region Ctors

        public SettingService(string settingsFilename)
            : base(settingsFilename)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilename);
            m_Lock = new();
            string? directory = Path.GetDirectoryName(SettingsFilename);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_UnableToDetermineUserSettingsDirectory);
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
            Data.ProjectPlan.v0_6_0.AppSettingsModel output = Converter.Format(m_AppSettingsModel);
            jsonSerializer.Serialize(writer, output, output.GetType());
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

        public override SortMode ProjectScenarioSortMode
        {
            get
            {
                return m_AppSettingsModel.ProjectScenarioSortMode;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { ProjectScenarioSortMode = value };
                    SaveSettings();
                }
            }
        }

        public override SortDirection ProjectScenarioSortDirection
        {
            get
            {
                return m_AppSettingsModel.ProjectScenarioSortDirection;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { ProjectScenarioSortDirection = value };
                    SaveSettings();
                }
            }
        }

        public override TrackedMetrics ScenarioChartTrackedMetricXAxis
        {
            get => m_AppSettingsModel.ScenarioChartTrackedMetricXAxis;
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { ScenarioChartTrackedMetricXAxis = value };
                    SaveSettings();
                    this.RaisePropertyChanged();
                }
            }
        }

        public override TrackedMetrics ScenarioChartTrackedMetricYAxis
        {
            get => m_AppSettingsModel.ScenarioChartTrackedMetricYAxis;
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { ScenarioChartTrackedMetricYAxis = value };
                    SaveSettings();
                    this.RaisePropertyChanged();
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
