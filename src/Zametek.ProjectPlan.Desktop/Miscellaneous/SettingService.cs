using Newtonsoft.Json;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Zametek.Common.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan.Desktop
{
    public class SettingService
        : SettingServiceBase
    {
        #region Fields

        private readonly Lock m_Lock;
        private string m_DockLayout;
        private readonly List<DataGridModel> m_DataGridLayouts;

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
            m_DataGridLayouts = [];
            string? directory = Path.GetDirectoryName(SettingsFilename);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_UnableToDetermineUserSettingsDirectory);
            }

            Directory.CreateDirectory(directory);

            if (SettingsReadFailure is not null)
            {
                Log.Error(
                    SettingsReadFailure,
                    "Could not read the settings file {SettingsFile}, and so the default settings are in use. The file is now {UnreadableSettingsFile}",
                    SettingsFilename,
                    UnreadableSettingsFilename ?? SettingsFilename);
            }

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
                        m_DataGridLayouts.AddRange(dataGridModels);
                    }
                }
                catch (Exception ex)
                {
                    // The data grid layout file is corrupt or unreadable - reset to defaults.
                    Log.Warning(
                        ex,
                        "Could not read the saved layout of the grids from {DataGridLayoutFile}, and so the default layout is in use",
                        DataGridLayoutFilename);
                    m_DataGridLayouts.Clear();
                }
            }
        }

        #endregion

        // Each of the three files goes to a temporary file first, which then takes the place of the file. Thus, a write
        // that does not finish leaves the file that was there (see FileStreamHelper.Replace).

        private void SaveDockLayout()
        {
            lock (m_Lock)
            {
                FileStreamHelper.Replace(DockLayoutFilename, stream =>
                {
                    using var writer = new StreamWriter(stream, leaveOpen: true);
                    writer.WriteLine(DockLayout);
                });
            }
        }

        private void SaveDataGridLayout()
        {
            lock (m_Lock)
            {
                FileStreamHelper.Replace(DataGridLayoutFilename, stream =>
                {
                    using var writer = new StreamWriter(stream, leaveOpen: true);
                    writer.WriteLine(JsonConvert.SerializeObject(m_DataGridLayouts, Formatting.Indented));
                });
            }
        }

        private void SaveSettings()
        {
            lock (m_Lock)
            {
                var jsonSerializer = JsonSerializer.Create(
                    new JsonSerializerSettings
                    {
                        Formatting = Formatting.Indented,
                        NullValueHandling = NullValueHandling.Ignore,
                    });
                Data.ProjectPlan.v0_6_1.AppSettingsModel output = Converter.Format(m_AppSettingsModel);

                FileStreamHelper.Replace(SettingsFilename, stream =>
                {
                    using var writer = new StreamWriter(stream, leaveOpen: true);
                    jsonSerializer.Serialize(writer, output, output.GetType());
                });
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

        public override IList<DataGridModel> GetDataGridLayout()
        {
            lock (m_Lock)
            {
                return [.. m_DataGridLayouts];
            }
        }

        public override void SetDataGridLayout(IList<DataGridModel> models)
        {
            lock (m_Lock)
            {
                m_DataGridLayouts.Clear();
                m_DataGridLayouts.AddRange(models);
                SaveDataGridLayout();
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

        public override int MaxRecentProjectFilePaths
        {
            get
            {
                return m_AppSettingsModel.MaxRecentProjectFilePaths;
            }
        }

        public override int CompilationTimeoutMilliseconds
        {
            get
            {
                return m_AppSettingsModel.CompilationTimeoutMilliseconds;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { CompilationTimeoutMilliseconds = value };
                    SaveSettings();
                }
            }
        }

        public override bool KeepRecoveryCopy
        {
            get
            {
                return m_AppSettingsModel.KeepRecoveryCopy;
            }
            set
            {
                lock (m_Lock)
                {
                    m_AppSettingsModel = m_AppSettingsModel with { KeepRecoveryCopy = value };
                    SaveSettings();
                }
            }
        }

        public override IReadOnlyList<string> RecentProjectFilePaths
        {
            get
            {
                // Newtonsoft can materialise a null here from a hand-edited settings
                // file, despite the non-nullable declaration.
                List<string>? recentPaths = m_AppSettingsModel.RecentProjectFilePaths;
                return recentPaths is null ? [] : [.. recentPaths];
            }
        }

        public override void RecordRecentProjectFilePath(string filename)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            lock (m_Lock)
            {
                List<string> recentPaths = RecentProjectFileHelper.Record(
                    m_AppSettingsModel.RecentProjectFilePaths,
                    filename,
                    m_AppSettingsModel.MaxRecentProjectFilePaths);
                m_AppSettingsModel = m_AppSettingsModel with { RecentProjectFilePaths = recentPaths };
                SaveSettings();
            }
        }

        public override void RemoveRecentProjectFilePath(string filename)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filename);
            lock (m_Lock)
            {
                List<string> recentPaths = RecentProjectFileHelper.Remove(
                    m_AppSettingsModel.RecentProjectFilePaths,
                    filename);
                m_AppSettingsModel = m_AppSettingsModel with { RecentProjectFilePaths = recentPaths };
                SaveSettings();
            }
        }

        public override void ClearRecentProjectFilePaths()
        {
            lock (m_Lock)
            {
                m_AppSettingsModel = m_AppSettingsModel with { RecentProjectFilePaths = [] };
                SaveSettings();
            }
        }

        #endregion
    }
}
