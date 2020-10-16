using System;
using System.Collections.Generic;
using System.IO;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Shell.ProjectPlan
{
    public class SettingService
        : ISettingService
    {
        #region Fields

        private static readonly double GoldenRatio = (1.0 + Math.Sqrt(5.0)) / 2.0;
        private string m_PlanTitle;

        #endregion

        #region ISettingService Members

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
                string directory = Settings.Default.ProjectPlanDirectory;
                return string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)
                    ? Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
                    : directory;
            }
            private set
            {
                Settings.Default.ProjectPlanDirectory = value;
                Settings.Default.Save();
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

        public ArrowGraphSettingsModel DefaultArrowGraphSettings =>
            new ArrowGraphSettingsModel
            {
                EdgeTypeFormats = new List<EdgeTypeFormatModel>(
                    new[]
                    {
                        new EdgeTypeFormatModel
                        {
                            EdgeType = EdgeType.Activity,
                            EdgeDashStyle = EdgeDashStyle.Normal,
                            EdgeWeightStyle = EdgeWeightStyle.Normal
                        },
                        new EdgeTypeFormatModel
                        {
                            EdgeType = EdgeType.CriticalActivity,
                            EdgeDashStyle = EdgeDashStyle.Normal,
                            EdgeWeightStyle = EdgeWeightStyle.Bold
                        },
                        new EdgeTypeFormatModel
                        {
                            EdgeType = EdgeType.Dummy,
                            EdgeDashStyle = EdgeDashStyle.Dashed,
                            EdgeWeightStyle = EdgeWeightStyle.Normal
                        },
                        new EdgeTypeFormatModel
                        {
                            EdgeType = EdgeType.CriticalDummy,
                            EdgeDashStyle = EdgeDashStyle.Dashed,
                            EdgeWeightStyle = EdgeWeightStyle.Bold
                        }
                    }),
                ActivitySeverities = new List<ActivitySeverityModel>(
                    new[]
                    {
                        // Black.
                        new ActivitySeverityModel
                        {
                            SlackLimit = 1,
                            CriticalityWeight = 4.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 3.0),
                            ColorFormat = new ColorFormatModel
                            {
                                A = 255,
                                R = 0,
                                G = 0,
                                B = 0
                            }
                        },
                        // Red.
                        new ActivitySeverityModel
                        {
                            SlackLimit = 9,
                            CriticalityWeight = 3.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 2.0),
                            ColorFormat = new ColorFormatModel
                            {
                                A = 255,
                                R = 255,
                                G = 0,
                                B = 0
                            }
                        },
                        // Gold.
                        new ActivitySeverityModel
                        {
                            SlackLimit = 25,
                            CriticalityWeight = 2.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 1.0),
                            ColorFormat = new ColorFormatModel
                            {
                                A = 255,
                                R = 255,
                                G = 215,
                                B = 0
                            }
                        },
                        // Green.
                        new ActivitySeverityModel
                        {
                            SlackLimit = int.MaxValue,
                            CriticalityWeight = 1.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 0.0),
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
            new ResourceSettingsModel
            {
                Resources = new List<ResourceModel>(),
                DefaultUnitCost = 1.0,
                AreDisabled = false
            };

        public void SetMainViewSettings(MainViewSettingsModel mainViewSettings)
        {
            if (mainViewSettings is null)
            {
                throw new ArgumentNullException(nameof(mainViewSettings));
            }

            Settings.Default.Main_Maximized = mainViewSettings.Maximized;
            Settings.Default.Main_Top = mainViewSettings.Top;
            Settings.Default.Main_Left = mainViewSettings.Left;
            Settings.Default.Main_Width = mainViewSettings.Width;
            Settings.Default.Main_Height = mainViewSettings.Height;
            Settings.Default.Save();
        }

        public MainViewSettingsModel MainViewSettings
        {
            get 
            {
                return new MainViewSettingsModel
                {
                    Maximized = Settings.Default.Main_Maximized,
                    Top = Settings.Default.Main_Top,
                    Left = Settings.Default.Main_Left,
                    Width = Settings.Default.Main_Width,
                    Height = Settings.Default.Main_Height,
                };
            }
        }

        public void Reset()
        {
            PlanTitle = null;
        }

        #endregion
    }
}
