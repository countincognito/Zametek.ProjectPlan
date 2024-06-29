using Microsoft.Extensions.Configuration.UserSecrets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.ViewModel.ProjectPlan;

namespace Zametek.ProjectPlan
{
    public class SettingService
        : ViewModelBase, ISettingService
    {
        #region Fields

        private readonly object m_Lock;
        private Data.ProjectPlan.v0_3_0.FileSettingsModel m_FileSettingsModel;

        private static readonly double s_GoldenRatio = (1.0 + Math.Sqrt(5.0)) / 2.0;

        #endregion

        #region Ctors

        public SettingService()
        {
            m_Lock = new object();
            m_ProjectTitle = string.Empty;

            string secretsId = Assembly.GetExecutingAssembly().GetCustomAttribute<UserSecretsIdAttribute>()!.UserSecretsId;
            SettingsFilename = PathHelper.GetSecretsPathFromSecretsId(secretsId);

            string? directory = Path.GetDirectoryName(SettingsFilename);

            if (string.IsNullOrWhiteSpace(directory))
            {
                throw new InvalidOperationException(Resource.ProjectPlan.Messages.Message_UnableToDetermineUserSecretsPath);
            }

            Directory.CreateDirectory(directory);

            m_FileSettingsModel = new Data.ProjectPlan.v0_3_0.FileSettingsModel();

            if (File.Exists(SettingsFilename))
            {
                using StreamReader reader = File.OpenText(SettingsFilename);
                string content = reader.ReadToEnd();
                JObject json = JObject.Parse(content);
                string version =
                    json?.GetValue(nameof(Data.ProjectPlan.v0_3_0.FileSettingsModel.Version), StringComparison.OrdinalIgnoreCase)?.ToString()
                    ?? string.Empty;
                string jsonString = json?.ToString() ?? string.Empty;
                m_FileSettingsModel =
                    JsonConvert.DeserializeObject<Data.ProjectPlan.v0_3_0.FileSettingsModel>(jsonString)
                    ?? new Data.ProjectPlan.v0_3_0.FileSettingsModel();
            }
        }

        #endregion

        private void Save()
        {
            using StreamWriter writer = File.CreateText(SettingsFilename);
            var jsonSerializer = JsonSerializer.Create(
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                });
            jsonSerializer.Serialize(writer, m_FileSettingsModel, m_FileSettingsModel.GetType());
        }

        #region ISettingService Members

        public string SettingsFilename { get; init; }

        private string m_ProjectTitle;
        public string ProjectTitle
        {
            get => string.IsNullOrWhiteSpace(m_ProjectTitle) ? string.Empty : m_ProjectTitle;
            private set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_ProjectTitle, value);
            }
        }

        public string ProjectDirectory
        {
            get
            {
                string directory = m_FileSettingsModel.ProjectPlanDirectory;
                return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                    : directory;
            }
            private set
            {
                lock (m_Lock)
                {
                    m_FileSettingsModel = m_FileSettingsModel with { ProjectPlanDirectory = value };
                    Save();
                }
            }
        }

        public void SetFilePath(string filename)//!!)
        {
            SetTitle(filename);
            SetDirectory(filename);
        }

        public void SetTitle(string filename)//!!)
        {
            ProjectTitle = Path.GetFileNameWithoutExtension(filename);
        }

        public void SetDirectory(string filename)//!!)
        {
            ProjectDirectory = Path.GetDirectoryName(filename) ?? string.Empty;
        }

        public ArrowGraphSettingsModel DefaultArrowGraphSettings =>
            new()
            {
                EdgeTypeFormats = new List<EdgeTypeFormatModel>(
                    new EdgeTypeFormatModel[]
                    {
                        new()
                        {
                            EdgeType = EdgeType.Activity,
                            EdgeDashStyle = EdgeDashStyle.Normal,
                            EdgeWeightStyle = EdgeWeightStyle.Normal
                        },
                        new()
                        {
                            EdgeType = EdgeType.CriticalActivity,
                            EdgeDashStyle = EdgeDashStyle.Normal,
                            EdgeWeightStyle = EdgeWeightStyle.Bold
                        },
                        new()
                        {
                            EdgeType = EdgeType.Dummy,
                            EdgeDashStyle = EdgeDashStyle.Dashed,
                            EdgeWeightStyle = EdgeWeightStyle.Normal
                        },
                        new()
                        {
                            EdgeType = EdgeType.CriticalDummy,
                            EdgeDashStyle = EdgeDashStyle.Dashed,
                            EdgeWeightStyle = EdgeWeightStyle.Bold
                        }
                    }),
                ActivitySeverities = new List<ActivitySeverityModel>(
                    new ActivitySeverityModel[]
                    {
                        // Black.
                        new()
                        {
                            SlackLimit = 1,
                            CriticalityWeight = 4.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 3.0),
                            ColorFormat = new ColorFormatModel
                            {
                                A = 255,
                                R = 0,
                                G = 0,
                                B = 0
                            }
                        },
                        // Red.
                        new()
                        {
                            SlackLimit = 9,
                            CriticalityWeight = 3.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 2.0),
                            ColorFormat = new ColorFormatModel
                            {
                                A = 255,
                                R = 255,
                                G = 0,
                                B = 0
                            }
                        },
                        // Gold.
                        new()
                        {
                            SlackLimit = 25,
                            CriticalityWeight = 2.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 1.0),
                            ColorFormat = new ColorFormatModel
                            {
                                A = 255,
                                R = 255,
                                G = 215,
                                B = 0
                            }
                        },
                        // Green.
                        new()
                        {
                            SlackLimit = int.MaxValue,
                            CriticalityWeight = 1.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 0.0),
                            ColorFormat = new ColorFormatModel
                            {
                                A = 255,
                                R = 0,
                                G = 128,
                                B = 0
                            }
                        }
                    })
            };

        public ResourceSettingsModel DefaultResourceSettings =>
            new()
            {
                DefaultUnitCost = 1.0,
                AreDisabled = false
            };

        public WorkStreamSettingsModel DefaultWorkStreamSettings => new();

        //public void SetMainViewSettings(MainViewSettingsModel mainViewSettings)
        //{
        //    if (mainViewSettings is null)
        //    {
        //        throw new ArgumentNullException(nameof(mainViewSettings));
        //    }

        //    Settings.Default.Main_Maximized = mainViewSettings.Maximized;
        //    Settings.Default.Main_Top = mainViewSettings.Top;
        //    Settings.Default.Main_Left = mainViewSettings.Left;
        //    Settings.Default.Main_Width = mainViewSettings.Width;
        //    Settings.Default.Main_Height = mainViewSettings.Height;
        //    Settings.Default.Save();
        //}

        //public MainViewSettingsModel MainViewSettings
        //{
        //    get 
        //    {
        //        return new MainViewSettingsModel
        //        {
        //            Maximized = Settings.Default.Main_Maximized,
        //            Top = Settings.Default.Main_Top,
        //            Left = Settings.Default.Main_Left,
        //            Width = Settings.Default.Main_Width,
        //            Height = Settings.Default.Main_Height,
        //        };
        //    }
        //}

        public void Reset()
        {
            ProjectTitle = string.Empty;
        }

        #endregion
    }
}
