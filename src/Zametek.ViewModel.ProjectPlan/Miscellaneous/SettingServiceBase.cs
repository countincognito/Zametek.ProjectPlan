using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Data.ProjectPlan;
using Zametek.Maths.Graphs;
using Zametek.Utility;

namespace Zametek.ViewModel.ProjectPlan
{
    public abstract class SettingServiceBase
        : ViewModelBase, ISettingService
    {
        #region Fields

        private readonly object m_Lock;
        protected AppSettingsModel m_AppSettingsModel;

        private static readonly double s_GoldenRatio = (1.0 + Math.Sqrt(5.0)) / 2.0;

        #endregion

        #region Ctors

        public SettingServiceBase(string settingsFilename)
        {
            m_Lock = new();
            m_ProjectTitle = string.Empty;
            m_AppSettingsModel = new()
            {
                Version = Versions.AppSettingsLatest,
            };
            SettingsFilename = settingsFilename;

            if (File.Exists(SettingsFilename))
            {
                using StreamReader reader = File.OpenText(SettingsFilename);
                string content = reader.ReadToEnd();
                JObject json = JObject.Parse(content);
                string version =
                    json?.GetValue(nameof(AppSettingsModel.Version), StringComparison.OrdinalIgnoreCase)?.ToString()
                    ?? string.Empty;
                string jsonString = json?.ToString() ?? string.Empty;

                Func<string, AppSettingsModel> func =
                    jString => new AppSettingsModel();

                version.ValueSwitchOn()
                    .Case(Versions.v0_3_0, x =>
                    {
                        func = jString => Converter.Upgrade(
                            JsonConvert.DeserializeObject<Data.ProjectPlan.v0_3_0.AppSettingsModel>(jString)
                            ?? new Data.ProjectPlan.v0_3_0.AppSettingsModel());
                    })
                    .Case(Versions.v0_4_1, x =>
                    {
                        func = jString => Converter.Upgrade(
                            JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_1.AppSettingsModel>(jString)
                            ?? new Data.ProjectPlan.v0_4_1.AppSettingsModel());
                    })
                    .Case(Versions.v0_4_4, x =>
                    {
                        func = jString => Converter.Upgrade(
                            JsonConvert.DeserializeObject<Data.ProjectPlan.v0_4_4.AppSettingsModel>(jString)
                            ?? new Data.ProjectPlan.v0_4_4.AppSettingsModel());
                    });

                m_AppSettingsModel = func(jsonString);
            }
        }

        #endregion

        #region ISettingService Members

        public string SettingsFilename { get; init; }

        private string m_ProjectTitle;
        public string ProjectTitle
        {
            get => string.IsNullOrWhiteSpace(m_ProjectTitle) ? string.Empty : m_ProjectTitle;
            protected set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_ProjectTitle, value);
            }
        }

        public abstract bool DefaultShowDates { get; set; }

        public abstract bool DefaultUseClassicDates { get; set; }

        public abstract bool DefaultUseBusinessDays { get; set; }

        public abstract bool DefaultHideCost { get; set; }

        public abstract bool DefaultHideBilling { get; set; }

        public abstract string SelectedTheme { get; set; }

        public abstract string ProjectDirectory { get; protected set; }

        public bool IsTitleBoundToFilename { get; set; }

        public void SetProjectFilePath(
            string filename,
            bool bindTitleToFilename)//!!)
        {
            SetProjectTitle(filename);
            SetProjectDirectory(filename);
            IsTitleBoundToFilename = bindTitleToFilename;
        }

        public void SetProjectTitle(string filename)//!!)
        {
            ProjectTitle = Path.GetFileNameWithoutExtension(filename).Trim();
        }

        public void SetProjectDirectory(string filename)//!!)
        {
            ProjectDirectory = Path.GetDirectoryName(filename) ?? string.Empty;
        }

        public ArrowGraphSettingsModel DefaultArrowGraphSettings =>
            new()
            {
                EdgeTypeFormats = new List<EdgeTypeFormatModel>(
                    [
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
                    ]),
                ActivitySeverities = new List<ActivitySeverityModel>(
                    [
                        // Black.
                        new()
                        {
                            SlackLimit = 1,
                            CriticalityWeight = 4.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 3.0),
                            ColorFormat = ColorHelper.Black()
                        },
                        // Red.
                        new()
                        {
                            SlackLimit = 9,
                            CriticalityWeight = 3.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 2.0),
                            ColorFormat = ColorHelper.Red()
                        },
                        // Gold.
                        new()
                        {
                            SlackLimit = 25,
                            CriticalityWeight = 2.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 1.0),
                            ColorFormat = ColorHelper.Gold()
                        },
                        // Green.
                        new()
                        {
                            SlackLimit = int.MaxValue,
                            CriticalityWeight = 1.0,
                            FibonacciWeight = Math.Pow(s_GoldenRatio, 0.0),
                            ColorFormat = ColorHelper.Green()
                        }
                    ])
            };

        public ResourceSettingsModel DefaultResourceSettings =>
            new()
            {
                DefaultUnitCost = 1.0,
                DefaultUnitBilling = 1.0,
                AreDisabled = false
            };

        public WorkStreamSettingsModel DefaultWorkStreamSettings => new();

        public void Reset()
        {
            ProjectTitle = string.Empty;
        }

        #endregion
    }
}
