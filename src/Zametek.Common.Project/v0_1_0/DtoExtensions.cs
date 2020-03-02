using System;
using System.Collections.Generic;
using System.Linq;

namespace Zametek.Common.Project.v0_1_0
{
    public static class DtoExtensions
    {
        private static Random _Rnd;

        static DtoExtensions()
        {
            _Rnd = new Random();
        }

        public static ColorFormatDto Randomize(this ColorFormatDto colorFormatDto)
        {
            if (colorFormatDto == null)
            {
                throw new ArgumentNullException(nameof(colorFormatDto));
            }
            var b = new byte[4];
            _Rnd.NextBytes(b);
            colorFormatDto.A = b[0];
            colorFormatDto.R = b[1];
            colorFormatDto.G = b[2];
            colorFormatDto.B = b[3];
            return colorFormatDto;
        }

        public static ColorFormatDto Copy(this ColorFormatDto colorFormatDto)
        {
            if (colorFormatDto == null)
            {
                throw new ArgumentNullException(nameof(colorFormatDto));
            }
            return new ColorFormatDto
            {
                A = colorFormatDto.A,
                R = colorFormatDto.R,
                G = colorFormatDto.G,
                B = colorFormatDto.B
            };
        }

        public static CircularDependencyDto Copy(this CircularDependencyDto circularDependencyDto)
        {
            if (circularDependencyDto == null)
            {
                throw new ArgumentNullException(nameof(circularDependencyDto));
            }
            return new CircularDependencyDto
            {
                Dependencies = circularDependencyDto.Dependencies != null ? circularDependencyDto.Dependencies.ToList() : new List<int>()
            };
        }

        public static ResourceDto Copy(this ResourceDto resourceDto)
        {
            if (resourceDto == null)
            {
                throw new ArgumentNullException(nameof(resourceDto));
            }
            return new ResourceDto
            {
                Id = resourceDto.Id,
                Name = resourceDto.Name,
                IsExplicitTarget = resourceDto.IsExplicitTarget,
                InterActivityAllocationType = resourceDto.InterActivityAllocationType,
                UnitCost = resourceDto.UnitCost,
                DisplayOrder = resourceDto.DisplayOrder,
                ColorFormat = resourceDto.ColorFormat != null ? resourceDto.ColorFormat.Copy() : new ColorFormatDto()
            };
        }

        public static ActivityDto Copy(this ActivityDto activityDto)
        {
            if (activityDto == null)
            {
                throw new ArgumentNullException(nameof(activityDto));
            }
            return new ActivityDto
            {
                Id = activityDto.Id,
                Name = activityDto.Name,
                TargetResources = activityDto.TargetResources != null ? activityDto.TargetResources.ToList() : new List<int>(),
                TargetResourceOperator = activityDto.TargetResourceOperator,
                CanBeRemoved = activityDto.CanBeRemoved,
                Duration = activityDto.Duration,
                FreeSlack = activityDto.FreeSlack,
                EarliestStartTime = activityDto.EarliestStartTime,
                LatestFinishTime = activityDto.LatestFinishTime,
                MinimumFreeSlack = activityDto.MinimumFreeSlack,
                MinimumEarliestStartTime = activityDto.MinimumEarliestStartTime,
                MinimumEarliestStartDateTime = activityDto.MinimumEarliestStartDateTime
            };
        }

        public static EventDto Copy(this EventDto eventDto)
        {
            if (eventDto == null)
            {
                throw new ArgumentNullException(nameof(eventDto));
            }
            return new EventDto
            {
                Id = eventDto.Id,
                EarliestFinishTime = eventDto.EarliestFinishTime,
                LatestFinishTime = eventDto.LatestFinishTime
            };
        }

        public static DependentActivityDto Copy(this DependentActivityDto dependentActivityDto)
        {
            if (dependentActivityDto == null)
            {
                throw new ArgumentNullException(nameof(dependentActivityDto));
            }
            return new DependentActivityDto
            {
                Activity = dependentActivityDto.Activity != null ? dependentActivityDto.Activity.Copy() : new ActivityDto() { TargetResources = new List<int>() },
                Dependencies = dependentActivityDto.Dependencies != null ? dependentActivityDto.Dependencies.ToList() : new List<int>(),
                ResourceDependencies = dependentActivityDto.ResourceDependencies != null ? dependentActivityDto.ResourceDependencies.ToList() : new List<int>()
            };
        }

        public static ArrowGraphSettingsDto Copy(this ArrowGraphSettingsDto arrowGraphSettingsDto)
        {
            if (arrowGraphSettingsDto == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphSettingsDto));
            }
            return new ArrowGraphSettingsDto
            {
                ActivitySeverities = arrowGraphSettingsDto.ActivitySeverities != null ? arrowGraphSettingsDto.ActivitySeverities.Select(x => x.Copy()).ToList() : new List<ActivitySeverityDto>(),
                EdgeTypeFormats = arrowGraphSettingsDto.EdgeTypeFormats != null ? arrowGraphSettingsDto.EdgeTypeFormats.Select(x => x.Copy()).ToList() : new List<EdgeTypeFormatDto>()
            };
        }

        public static ResourceSettingsDto Copy(this ResourceSettingsDto resourceSettingsDto)
        {
            if (resourceSettingsDto == null)
            {
                throw new ArgumentNullException(nameof(resourceSettingsDto));
            }
            return new ResourceSettingsDto
            {
                Resources = resourceSettingsDto.Resources != null ? resourceSettingsDto.Resources.Select(x => x.Copy()).ToList() : new List<ResourceDto>(),
                DefaultUnitCost = resourceSettingsDto.DefaultUnitCost,
                AreDisabled = resourceSettingsDto.AreDisabled
            };
        }

        public static ActivitySeverityDto Copy(this ActivitySeverityDto activitySeverityDto)
        {
            if (activitySeverityDto == null)
            {
                throw new ArgumentNullException(nameof(activitySeverityDto));
            }
            return new ActivitySeverityDto
            {
                SlackLimit = activitySeverityDto.SlackLimit,
                CriticalityWeight = activitySeverityDto.CriticalityWeight,
                FibonacciWeight = activitySeverityDto.FibonacciWeight,
                ColorFormat = activitySeverityDto.ColorFormat != null ? activitySeverityDto.ColorFormat.Copy() : new ColorFormatDto()
            };
        }

        public static EdgeTypeFormatDto Copy(this EdgeTypeFormatDto edgeTypeFormatDto)
        {
            if (edgeTypeFormatDto == null)
            {
                throw new ArgumentNullException(nameof(edgeTypeFormatDto));
            }
            return new EdgeTypeFormatDto
            {
                EdgeDashStyle = edgeTypeFormatDto.EdgeDashStyle,
                EdgeType = edgeTypeFormatDto.EdgeType,
                EdgeWeightStyle = edgeTypeFormatDto.EdgeWeightStyle
            };
        }

        public static ActivityEdgeDto Copy(this ActivityEdgeDto activityEdgeDto)
        {
            if (activityEdgeDto == null)
            {
                throw new ArgumentNullException(nameof(activityEdgeDto));
            }
            return new ActivityEdgeDto
            {
                Content = activityEdgeDto.Content != null ? activityEdgeDto.Content.Copy() : new ActivityDto()
            };
        }

        public static EventNodeDto Copy(this EventNodeDto eventNodeDto)
        {
            if (eventNodeDto == null)
            {
                throw new ArgumentNullException(nameof(eventNodeDto));
            }
            return new EventNodeDto
            {
                NodeType = eventNodeDto.NodeType,
                Content = eventNodeDto.Content != null ? eventNodeDto.Content.Copy() : new EventDto(),
                IncomingEdges = eventNodeDto.IncomingEdges != null ? eventNodeDto.IncomingEdges.ToList() : new List<int>(),
                OutgoingEdges = eventNodeDto.OutgoingEdges != null ? eventNodeDto.OutgoingEdges.ToList() : new List<int>()
            };
        }

        public static ArrowGraphDto Copy(this ArrowGraphDto arrowGraphDto)
        {
            if (arrowGraphDto == null)
            {
                throw new ArgumentNullException(nameof(arrowGraphDto));
            }
            return new ArrowGraphDto
            {
                Edges = arrowGraphDto.Edges != null ? arrowGraphDto.Edges.Select(x => x.Copy()).ToList() : new List<ActivityEdgeDto>(),
                Nodes = arrowGraphDto.Nodes != null ? arrowGraphDto.Nodes.Select(x => x.Copy()).ToList() : new List<EventNodeDto>(),
                IsStale = arrowGraphDto.IsStale
            };
        }
    }
}
