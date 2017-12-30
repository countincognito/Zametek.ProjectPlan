using System;
using System.Collections.Generic;
using Zametek.Common.Project;
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

        public ArrowGraphSettingsDto GetArrowGraphSettings()
        {
            return new ArrowGraphSettingsDto
            {
                EdgeTypeFormats = new List<EdgeTypeFormatDto>(
                    new[]
                    {
                        new EdgeTypeFormatDto
                        {
                            EdgeType = EdgeType.Activity,
                            EdgeDashStyle = EdgeDashStyle.Normal,
                            EdgeWeightStyle = EdgeWeightStyle.Normal
                        },
                        new EdgeTypeFormatDto
                        {
                            EdgeType = EdgeType.CriticalActivity,
                            EdgeDashStyle = EdgeDashStyle.Normal,
                            EdgeWeightStyle = EdgeWeightStyle.Bold
                        },
                        new EdgeTypeFormatDto
                        {
                            EdgeType = EdgeType.Dummy,
                            EdgeDashStyle = EdgeDashStyle.Dashed,
                            EdgeWeightStyle = EdgeWeightStyle.Normal
                        },
                        new EdgeTypeFormatDto
                        {
                            EdgeType = EdgeType.CriticalDummy,
                            EdgeDashStyle = EdgeDashStyle.Dashed,
                            EdgeWeightStyle = EdgeWeightStyle.Bold
                        }
                    }),
                ActivitySeverities = new List<ActivitySeverityDto>(
                    new[]
                    {
                        // Black.
                        new ActivitySeverityDto
                        {
                            SlackLimit = 1,
                            CriticalityWeight = 4.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 3.0),
                            ColorFormat = new ColorFormatDto
                            {
                                A = 255,
                                R = 0,
                                G = 0,
                                B = 0
                            }
                        },
                        // Red.
                        new ActivitySeverityDto
                        {
                            SlackLimit = 9,
                            CriticalityWeight = 3.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 2.0),
                            ColorFormat = new ColorFormatDto
                            {
                                A = 255,
                                R = 255,
                                G = 0,
                                B = 0
                            }
                        },
                        // Gold.
                        new ActivitySeverityDto
                        {
                            SlackLimit = 25,
                            CriticalityWeight = 2.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 1.0),
                            ColorFormat = new ColorFormatDto
                            {
                                A = 255,
                                R = 255,
                                G = 215,
                                B = 0
                            }
                        },
                        // Green.
                        new ActivitySeverityDto
                        {
                            SlackLimit = int.MaxValue,
                            CriticalityWeight = 1.0,
                            FibonacciWeight = Math.Pow(GoldenRatio, 0.0),
                            ColorFormat = new ColorFormatDto
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

        public ResourceSettingsDto GetResourceSettings()
        {
            return new ResourceSettingsDto
            {
                Resources = new List<ResourceDto>(),
                DefaultUnitCost = 1.0,
                AreDisabled = false
            };
        }

        #endregion
    }
}
