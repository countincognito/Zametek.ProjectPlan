using AutoMapper;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan
{
    public class MapperProfile
        : Profile
    {
        public MapperProfile()
        {
            #region v0.1.0

            CreateMap<ActivityEdgeModel, v0_1_0.ActivityEdgeModel>().ReverseMap();
            CreateMap<ActivityModel, v0_1_0.ActivityModel>().ReverseMap();
            CreateMap<ActivityNodeModel, v0_1_0.ActivityNodeModel>().ReverseMap();
            CreateMap<ScheduledActivityModel, v0_1_0.ScheduledActivityModel>().ReverseMap();

            CreateMap<CircularDependencyModel, v0_1_0.CircularDependencyModel>().ReverseMap();
            CreateMap<DependentActivityModel, v0_1_0.DependentActivityModel>().ReverseMap();

            CreateMap<ColorFormatModel, v0_1_0.ColorFormatModel>().ReverseMap();
            CreateMap<EdgeDashStyle, v0_1_0.EdgeDashStyle>().ReverseMap();
            CreateMap<EdgeTypeFormatModel, v0_1_0.EdgeTypeFormatModel>().ReverseMap();
            CreateMap<EdgeWeightStyle, v0_1_0.EdgeWeightStyle>().ReverseMap();

            CreateMap<EventEdgeModel, v0_1_0.EventEdgeModel>().ReverseMap();
            CreateMap<EventModel, v0_1_0.EventModel>().ReverseMap();
            CreateMap<EventNodeModel, v0_1_0.EventNodeModel>().ReverseMap();

            CreateMap<ArrowGraphModel, v0_1_0.ArrowGraphModel>().ReverseMap();
            //CreateMap<GraphCompilationModel, v0_1_0.GraphCompilationModel>().ReverseMap();
            CreateMap<VertexGraphModel, v0_1_0.VertexGraphModel>().ReverseMap();

            //CreateMap<ProjectPlanModel, v0_1_0.ProjectPlanModel>().ReverseMap();

            CreateMap<ResourceModel, v0_1_0.ResourceModel>().ReverseMap();
            CreateMap<ResourceScheduleModel, v0_1_0.ResourceScheduleModel>().ReverseMap();

            CreateMap<ActivitySeverityModel, v0_1_0.ActivitySeverityModel>().ReverseMap();
            CreateMap<ArrowGraphSettingsModel, v0_1_0.ArrowGraphSettingsModel>().ReverseMap();
            CreateMap<ResourceSettingsModel, v0_1_0.ResourceSettingsModel>().ReverseMap();

            #endregion

            #region v0.2.0

            CreateMap<GraphCompilationErrorsModel, v0_2_0.GraphCompilationErrorsModel>().ReverseMap();
            CreateMap<GraphCompilationModel, v0_2_0.GraphCompilationModel>().ReverseMap();

            CreateMap<ProjectPlanModel, v0_2_0.ProjectPlanModel>().ReverseMap();

            #endregion

            #region v0.2.1

            CreateMap<ActivityEdgeModel, v0_2_1.ActivityEdgeModel>().ReverseMap();
            CreateMap<ActivityModel, v0_2_1.ActivityModel>().ReverseMap();
            CreateMap<ActivityNodeModel, v0_2_1.ActivityNodeModel>().ReverseMap();
            CreateMap<ScheduledActivityModel, v0_2_1.ScheduledActivityModel>().ReverseMap();
            CreateMap<v0_1_0.ActivityEdgeModel, v0_2_1.ActivityEdgeModel>().ReverseMap();
            CreateMap<v0_1_0.ActivityModel, v0_2_1.ActivityModel>().ReverseMap();
            CreateMap<v0_1_0.ActivityNodeModel, v0_2_1.ActivityNodeModel>().ReverseMap();
            CreateMap<v0_1_0.ScheduledActivityModel, v0_2_1.ScheduledActivityModel>().ReverseMap();

            CreateMap<DependentActivityModel, v0_2_1.DependentActivityModel>().ReverseMap();
            CreateMap<v0_1_0.DependentActivityModel, v0_2_1.DependentActivityModel>().ReverseMap();

            CreateMap<ArrowGraphModel, v0_2_1.ArrowGraphModel>().ReverseMap();
            CreateMap<GraphCompilationErrorsModel, v0_2_1.GraphCompilationErrorsModel>().ReverseMap();
            CreateMap<GraphCompilationModel, v0_2_1.GraphCompilationModel>().ReverseMap();
            CreateMap<VertexGraphModel, v0_2_1.VertexGraphModel>().ReverseMap();
            CreateMap<v0_1_0.ArrowGraphModel, v0_2_1.ArrowGraphModel>().ReverseMap();
            CreateMap<v0_1_0.VertexGraphModel, v0_2_1.VertexGraphModel>().ReverseMap();

            CreateMap<ResourceModel, v0_2_1.ResourceModel>().ReverseMap();
            CreateMap<ResourceScheduleModel, v0_2_1.ResourceScheduleModel>().ReverseMap();
            CreateMap<v0_1_0.ResourceModel, v0_2_1.ResourceModel>().ReverseMap();
            CreateMap<v0_1_0.ResourceScheduleModel, v0_2_1.ResourceScheduleModel>().ReverseMap();

            CreateMap<ProjectPlanModel, v0_2_1.ProjectPlanModel>().ReverseMap();

            #endregion
        }
    }
}
