using System;
using System.Collections.Generic;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Access.ProjectPlan
{
    public class SettingResourceAccess
        : ISettingResourceAccess
    {
        #region Fields

        private static readonly double GoldenRatio = (1.0 + Math.Sqrt(5.0)) / 2.0;

        #endregion

        #region ISettingResourceAccess Members

        public Common.Project.v0_1_0.ArrowGraphSettingsDto GetArrowGraphSettings()
        {
            return new Common.Project.v0_1_0.ArrowGraphSettingsDto
            {
                EdgeTypeFormats = new List<Common.Project.v0_1_0.EdgeTypeFormatDto>(
                    new[]
                    {
                        new Common.Project.v0_1_0.EdgeTypeFormatDto
                        {
                            EdgeType = EdgeType.Activity,
                            EdgeDashStyle = Common.Project.v0_1_0.EdgeDashStyle.Normal,
                            EdgeWeightStyle = Common.Project.v0_1_0.EdgeWeightStyle.Normal
                        },
                        new Common.Project.v0_1_0.EdgeTypeFormatDto
                        {
                            EdgeType = EdgeType.CriticalActivity,
                            EdgeDashStyle = Common.Project.v0_1_0.EdgeDashStyle.Normal,
                            EdgeWeightStyle = Common.Project.v0_1_0.EdgeWeightStyle.Bold
                        },
                        new Common.Project.v0_1_0.EdgeTypeFormatDto
                        {
                            EdgeType = EdgeType.Dummy,
                            EdgeDashStyle = Common.Project.v0_1_0.EdgeDashStyle.Dashed,
                            EdgeWeightStyle = Common.Project.v0_1_0.EdgeWeightStyle.Normal
                        },
                        new Common.Project.v0_1_0.EdgeTypeFormatDto
                        {
                            EdgeType = EdgeType.CriticalDummy,
                            EdgeDashStyle = Common.Project.v0_1_0.EdgeDashStyle.Dashed,
                            EdgeWeightStyle = Common.Project.v0_1_0.EdgeWeightStyle.Bold
                        }
                    }),
                ActivitySeverities = new List<Common.Project.v0_1_0.ActivitySeverityDto>(
                    new[]
                    {
                        // Black.
                        new Common.Project.v0_1_0.ActivitySeverityDto
                        {
                            SlackLimit = 1,
                            CriticalityWeight = 4.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 3.0),
                            ColorFormat = new Common.Project.v0_1_0.ColorFormatDto
                            {
                                A = 255,
                                R = 0,
                                G = 0,
                                B = 0
                            }
                        },
                        // Red.
                        new Common.Project.v0_1_0.ActivitySeverityDto
                        {
                            SlackLimit = 9,
                            CriticalityWeight = 3.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 2.0),
                            ColorFormat = new Common.Project.v0_1_0.ColorFormatDto
                            {
                                A = 255,
                                R = 255,
                                G = 0,
                                B = 0
                            }
                        },
                        // Gold.
                        new Common.Project.v0_1_0.ActivitySeverityDto
                        {
                            SlackLimit = 25,
                            CriticalityWeight = 2.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 1.0),
                            ColorFormat = new Common.Project.v0_1_0.ColorFormatDto
                            {
                                A = 255,
                                R = 255,
                                G = 215,
                                B = 0
                            }
                        },
                        // Green.
                        new Common.Project.v0_1_0.ActivitySeverityDto
                        {
                            SlackLimit = int.MaxValue,
                            CriticalityWeight = 1.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 0.0),
                            ColorFormat = new Common.Project.v0_1_0.ColorFormatDto
                            {
                                A = 255,
                                R = 0,
                                G = 128,
                                B = 0
                            }
                        }
                    })
            };
        }

        public Common.Project.v0_1_0.ResourceSettingsDto GetResourceSettings()
        {
            return new Common.Project.v0_1_0.ResourceSettingsDto
            {
                Resources = new List<Common.Project.v0_1_0.ResourceDto>(),
                DefaultUnitCost = 1.0,
                AreDisabled = false
            };
        }

        #endregion
    }
}
