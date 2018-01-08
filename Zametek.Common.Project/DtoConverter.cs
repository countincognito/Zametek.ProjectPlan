using System;
using System.Collections.Generic;
using System.Linq;
using Zametek.Maths.Graphs;

namespace Zametek.Common.Project
{
    public static class DtoConverter
    {
        #region Hex Colors

        public static string HexConverter(byte r, byte g, byte b)
        {
            return $@"#{r.ToString("X2")}{g.ToString("X2")}{b.ToString("X2")}";
        }

        public static string HexConverter(byte a, byte r, byte g, byte b)
        {
            return $@"#{a.ToString("X2")}{r.ToString("X2")}{g.ToString("X2")}{b.ToString("X2")}";
        }

        public static string HexConverter(ColorFormatDto colorFormatDto)
        {
            if (colorFormatDto == null)
            {
                throw new ArgumentNullException(nameof(colorFormatDto));
            }
            return HexConverter(colorFormatDto.A, colorFormatDto.R, colorFormatDto.G, colorFormatDto.B);
        }

        #endregion

        #region FromDto

        public static IActivity<int> FromDto(ActivityDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            return new Activity<int>(
                dto.Id,
                dto.Name,
                dto.TargetResources,
                dto.TargetResourceOperator,
                dto.CanBeRemoved,
                dto.Duration,
                dto.FreeSlack,
                dto.EarliestStartTime,
                dto.LatestFinishTime,
                dto.MinimumFreeSlack,
                dto.MinimumEarliestStartTime,
                dto.MinimumEarliestStartDateTime);
        }

        public static IDependentActivity<int> FromDto(DependentActivityDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            return ToDependentActivity(FromDto(dto.Activity), dto.Dependencies, dto.ResourceDependencies);
        }

        public static IDependentActivity<int> ToDependentActivity(
            IActivity<int> activity,
            IEnumerable<int> dependencies,
            IEnumerable<int> resourceDependencies)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }
            if (resourceDependencies == null)
            {
                throw new ArgumentNullException(nameof(resourceDependencies));
            }
            return new DependentActivity<int>(
                activity.Id,
                activity.Name,
                activity.TargetResources,
                dependencies,
                resourceDependencies,
                activity.TargetResourceOperator,
                activity.CanBeRemoved,
                activity.Duration,
                activity.FreeSlack,
                activity.EarliestStartTime,
                activity.LatestFinishTime,
                activity.MinimumFreeSlack,
                activity.MinimumEarliestStartTime,
                activity.MinimumEarliestStartDateTime);
        }

        public static CircularDependency<int> FromDto(CircularDependencyDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            return new CircularDependency<int>(dto.Dependencies);
        }

        public static IResource<int> FromDto(ResourceDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            return new Resource<int>(
                dto.Id,
                dto.Name,
                dto.IsExplicitTarget,
                dto.InterActivityAllocationType,
                dto.UnitCost,
                dto.DisplayOrder);
        }

        public static IScheduledActivity<int> FromDto(ScheduledActivityDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            return new ScheduledActivity<int>(dto.Id, dto.Name, dto.Duration, dto.StartTime, dto.FinishTime);
        }

        public static IResourceSchedule<int> FromDto(ResourceScheduleDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            return new ResourceSchedule<int>(
                dto.Resource != null ? FromDto(dto.Resource) : null,
                dto.ScheduledActivities.Select(x => FromDto(x)),
                dto.FinishTime);
        }

        public static GraphCompilation<int, IDependentActivity<int>> FromDto(GraphCompilationDto dto)
        {
            if (dto == null)
            {
                throw new ArgumentNullException(nameof(dto));
            }
            return new GraphCompilation<int, IDependentActivity<int>>(
                dto.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                dto.CircularDependencies.Select(x => FromDto(x)),
                dto.MissingDependencies,
                dto.DependentActivities.Select(x => FromDto(x)),
                dto.ResourceSchedules.Select(x => FromDto(x)));
        }

        #endregion

        #region ToDto

        public static ActivityDto ToDto(IActivity<int> activity)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }
            return new ActivityDto
            {
                Id = activity.Id,
                Name = activity.Name,
                TargetResources = activity.TargetResources.ToList(),
                TargetResourceOperator = activity.TargetResourceOperator,
                CanBeRemoved = activity.CanBeRemoved,
                Duration = activity.Duration,
                FreeSlack = activity.FreeSlack,
                EarliestStartTime = activity.EarliestStartTime,
                LatestFinishTime = activity.LatestFinishTime,
                MinimumFreeSlack = activity.MinimumFreeSlack,
                MinimumEarliestStartTime = activity.MinimumEarliestStartTime,
                MinimumEarliestStartDateTime = activity.MinimumEarliestStartDateTime
            };
        }

        public static EventDto ToDto(IEvent<int> eventVertex)
        {
            if (eventVertex == null)
            {
                throw new ArgumentNullException(nameof(eventVertex));
            }
            return new EventDto
            {
                Id = eventVertex.Id,
                EarliestFinishTime = eventVertex.EarliestFinishTime,
                LatestFinishTime = eventVertex.LatestFinishTime
            };
        }

        public static ActivityEdgeDto ToDto(Edge<int, IActivity<int>> edge)
        {
            if (edge == null)
            {
                throw new ArgumentNullException(nameof(edge));
            }
            return new ActivityEdgeDto
            {
                Content = ToDto(edge.Content)
            };
        }

        public static ActivityEdgeDto ToDto(Edge<int, IDependentActivity<int>> edge)
        {
            if (edge == null)
            {
                throw new ArgumentNullException(nameof(edge));
            }
            return new ActivityEdgeDto
            {
                Content = ToDto((IActivity<int>)edge.Content)
            };
        }

        public static EventNodeDto ToDto(Node<int, IEvent<int>> node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            return new EventNodeDto
            {
                NodeType = node.NodeType,
                Content = ToDto(node.Content),
                IncomingEdges = node.NodeType != NodeType.Start && node.NodeType != NodeType.Isolated ? node.IncomingEdges.ToList() : new List<int>(),
                OutgoingEdges = node.NodeType != NodeType.End && node.NodeType != NodeType.Isolated ? node.OutgoingEdges.ToList() : new List<int>()
            };
        }

        public static ArrowGraphDto ToDto(Graph<int, IActivity<int>, IEvent<int>> arrowGraph)
        {
            if (arrowGraph == null)
            {
                throw new ArgumentNullException(nameof(arrowGraph));
            }
            return new ArrowGraphDto
            {
                Edges = arrowGraph.Edges.Select(ToDto).ToList(),
                Nodes = arrowGraph.Nodes.Select(ToDto).ToList()
            };
        }

        public static ArrowGraphDto ToDto(Graph<int, IDependentActivity<int>, IEvent<int>> arrowGraph)
        {
            if (arrowGraph == null)
            {
                throw new ArgumentNullException(nameof(arrowGraph));
            }
            return new ArrowGraphDto
            {
                Edges = arrowGraph.Edges.Select(ToDto).ToList(),
                Nodes = arrowGraph.Nodes.Select(ToDto).ToList()
            };
        }

        public static EventEdgeDto ToDto(Edge<int, IEvent<int>> edge)
        {
            if (edge == null)
            {
                throw new ArgumentNullException(nameof(edge));
            }
            return new EventEdgeDto
            {
                Content = ToDto(edge.Content)
            };
        }

        public static ActivityNodeDto ToDto(Node<int, IActivity<int>> node)
        {
            if (node == null)
            {
                throw new ArgumentNullException(nameof(node));
            }
            return new ActivityNodeDto
            {
                NodeType = node.NodeType,
                Content = ToDto(node.Content),
                IncomingEdges = node.NodeType != NodeType.Start && node.NodeType != NodeType.Isolated ? node.IncomingEdges.ToList() : new List<int>(),
                OutgoingEdges = node.NodeType != NodeType.End && node.NodeType != NodeType.Isolated ? node.OutgoingEdges.ToList() : new List<int>()
            };
        }

        public static VertexGraphDto ToDto(Graph<int, IEvent<int>, IActivity<int>> vertexGraph)
        {
            if (vertexGraph == null)
            {
                throw new ArgumentNullException(nameof(vertexGraph));
            }
            return new VertexGraphDto
            {
                Edges = vertexGraph.Edges.Select(ToDto).ToList(),
                Nodes = vertexGraph.Nodes.Select(ToDto).ToList()
            };
        }

        public static DependentActivityDto ToDto(IDependentActivity<int> dependentActivity)
        {
            if (dependentActivity == null)
            {
                throw new ArgumentNullException(nameof(dependentActivity));
            }
            return new DependentActivityDto
            {
                Activity = ToDto(dependentActivity as IActivity<int>),
                Dependencies = dependentActivity.Dependencies.ToList(),
                ResourceDependencies = dependentActivity.ResourceDependencies.ToList()
            };
        }

        public static CircularDependencyDto ToDto(CircularDependency<int> circularDependency)
        {
            if (circularDependency == null)
            {
                throw new ArgumentNullException(nameof(circularDependency));
            }
            return new CircularDependencyDto
            {
                Dependencies = circularDependency.Dependencies.ToList()
            };
        }

        public static ScheduledActivityDto ToDto(IScheduledActivity<int> scheduledActivity)
        {
            if (scheduledActivity == null)
            {
                throw new ArgumentNullException(nameof(scheduledActivity));
            }
            return new ScheduledActivityDto
            {
                Id = scheduledActivity.Id,
                Name = scheduledActivity.Name,
                Duration = scheduledActivity.Duration,
                StartTime = scheduledActivity.StartTime,
                FinishTime = scheduledActivity.FinishTime
            };
        }

        public static GraphCompilationDto ToDto(
            GraphCompilation<int, IDependentActivity<int>> graphCompilation,
            int cyclomaticComplexity,
            int duration)
        {
            if (graphCompilation == null)
            {
                throw new ArgumentNullException(nameof(graphCompilation));
            }
            return new GraphCompilationDto
            {
                AllResourcesExplicitTargetsButNotAllActivitiesTargeted = graphCompilation.AllResourcesExplicitTargetsButNotAllActivitiesTargeted,
                CircularDependencies = graphCompilation.CircularDependencies != null ? graphCompilation.CircularDependencies.Select(x => ToDto(x)).ToList() : new List<CircularDependencyDto>(),
                MissingDependencies = graphCompilation.MissingDependencies != null ? graphCompilation.MissingDependencies.ToList() : new List<int>(),
                DependentActivities = graphCompilation.DependentActivities != null ? graphCompilation.DependentActivities.Select(x => ToDto(x)).ToList() : new List<DependentActivityDto>(),
                ResourceSchedules = graphCompilation.ResourceSchedules != null ? graphCompilation.ResourceSchedules.Select(x => ToDto(x)).ToList() : new List<ResourceScheduleDto>(),
                CyclomaticComplexity = cyclomaticComplexity,
                Duration = duration
            };
        }

        public static ResourceDto ToDto(IResource<int> resource)
        {
            if (resource == null)
            {
                throw new ArgumentNullException(nameof(resource));
            }
            return new ResourceDto
            {
                Id = resource.Id,
                Name = resource.Name,
                IsExplicitTarget = resource.IsExplicitTarget,
                InterActivityAllocationType = resource.InterActivityAllocationType,
                UnitCost = resource.UnitCost,
                DisplayOrder = resource.DisplayOrder,
                ColorFormat = new ColorFormatDto()
            };
        }

        public static ResourceScheduleDto ToDto(IResourceSchedule<int> resourceSchedule)
        {
            if (resourceSchedule == null)
            {
                throw new ArgumentNullException(nameof(resourceSchedule));
            }
            return new ResourceScheduleDto
            {
                Resource = resourceSchedule.Resource != null ? ToDto(resourceSchedule.Resource) : null,
                ScheduledActivities = resourceSchedule.ScheduledActivities.Select(x => ToDto(x)).ToList(),
                FinishTime = resourceSchedule.FinishTime
            };
        }

        #endregion
    }
}
