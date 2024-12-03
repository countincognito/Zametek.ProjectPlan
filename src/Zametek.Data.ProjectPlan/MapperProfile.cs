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

            CreateMap<DependentActivityModel, v0_1_0.DependentActivityModel>().ReverseMap();

            CreateMap<ColorFormatModel, v0_1_0.ColorFormatModel>().ReverseMap();
            CreateMap<EdgeDashStyle, v0_1_0.EdgeDashStyle>().ReverseMap();
            CreateMap<EdgeTypeFormatModel, v0_1_0.EdgeTypeFormatModel>().ReverseMap();
            CreateMap<EdgeWeightStyle, v0_1_0.EdgeWeightStyle>().ReverseMap();

            CreateMap<EventEdgeModel, v0_1_0.EventEdgeModel>().ReverseMap();
            CreateMap<EventModel, v0_1_0.EventModel>().ReverseMap();
            CreateMap<EventNodeModel, v0_1_0.EventNodeModel>().ReverseMap();

            CreateMap<ArrowGraphModel, v0_1_0.ArrowGraphModel>().ReverseMap();
            CreateMap<VertexGraphModel, v0_1_0.VertexGraphModel>().ReverseMap();

            CreateMap<ResourceModel, v0_1_0.ResourceModel>().ReverseMap();
            CreateMap<ResourceScheduleModel, v0_1_0.ResourceScheduleModel>().ReverseMap();

            CreateMap<ActivitySeverityModel, v0_1_0.ActivitySeverityModel>().ReverseMap();
            CreateMap<ArrowGraphSettingsModel, v0_1_0.ArrowGraphSettingsModel>().ReverseMap();
            CreateMap<ResourceSettingsModel, v0_1_0.ResourceSettingsModel>().ReverseMap();

            #endregion

            #region v0.2.0

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
            CreateMap<GraphCompilationModel, v0_2_1.GraphCompilationModel>().ReverseMap();
            CreateMap<VertexGraphModel, v0_2_1.VertexGraphModel>().ReverseMap();
            CreateMap<v0_1_0.ArrowGraphModel, v0_2_1.ArrowGraphModel>().ReverseMap();
            CreateMap<v0_1_0.VertexGraphModel, v0_2_1.VertexGraphModel>().ReverseMap();

            CreateMap<ResourceModel, v0_2_1.ResourceModel>().ReverseMap();
            CreateMap<ResourceScheduleModel, v0_2_1.ResourceScheduleModel>().ReverseMap();
            CreateMap<v0_1_0.ResourceModel, v0_2_1.ResourceModel>().ReverseMap();
            CreateMap<v0_1_0.ResourceScheduleModel, v0_2_1.ResourceScheduleModel>().ReverseMap();

            #endregion

            #region v0.3.0

            CreateMap<ActivityEdgeModel, v0_3_0.ActivityEdgeModel>().ReverseMap();
            CreateMap<v0_2_1.ActivityEdgeModel, v0_3_0.ActivityEdgeModel>().ReverseMap();

            CreateMap<ActivityNodeModel, v0_3_0.ActivityNodeModel>().ReverseMap();
            CreateMap<v0_2_1.ActivityNodeModel, v0_3_0.ActivityNodeModel>().ReverseMap();

            CreateMap<ActivityModel, v0_3_0.ActivityModel>().ReverseMap();
            CreateMap<v0_2_1.ActivityModel, v0_3_0.ActivityModel>().ReverseMap();

            CreateMap<ActivityTrackerModel, v0_3_0.TrackerModel>().ReverseMap();

            CreateMap<DependentActivityModel, v0_3_0.DependentActivityModel>().ReverseMap();
            CreateMap<v0_2_1.DependentActivityModel, v0_3_0.DependentActivityModel>().ReverseMap();

            CreateMap<EventNodeModel, v0_3_0.EventNodeModel>().ReverseMap();
            CreateMap<v0_1_0.EventNodeModel, v0_3_0.EventNodeModel>().ReverseMap();

            CreateMap<ArrowGraphModel, v0_3_0.ArrowGraphModel>().ReverseMap();
            CreateMap<v0_2_1.ArrowGraphModel, v0_3_0.ArrowGraphModel>().ReverseMap();
            CreateMap<GraphCompilationErrorModel, v0_3_0.GraphCompilationErrorModel>().ReverseMap();
            CreateMap<GraphCompilationModel, v0_3_0.GraphCompilationModel>().ReverseMap();

            CreateMap<ResourceModel, v0_3_0.ResourceModel>().ReverseMap();
            CreateMap<ResourceScheduleModel, v0_3_0.ResourceScheduleModel>().ReverseMap();
            CreateMap<v0_2_1.ResourceModel, v0_3_0.ResourceModel>()
                .ForMember(dest => dest.ColorFormat, opt => opt.Condition(src => src.ColorFormat is not null))
                .ReverseMap();
            CreateMap<v0_2_1.ResourceScheduleModel, v0_3_0.ResourceScheduleModel>().ReverseMap();

            CreateMap<ProjectPlanModel, v0_3_0.ProjectPlanModel>().ReverseMap();

            #endregion

            #region v0.3.1

            CreateMap<GraphCompilationModel, v0_3_1.GraphCompilationModel>().ReverseMap();
            CreateMap<v0_3_0.GraphCompilationModel, v0_3_1.GraphCompilationModel>().ReverseMap();

            CreateMap<ResourceModel, v0_3_1.ResourceModel>().ReverseMap();
            CreateMap<v0_1_0.ResourceModel, v0_3_1.ResourceModel>().ReverseMap();
            CreateMap<v0_3_0.ResourceModel, v0_3_1.ResourceModel>().ReverseMap();
            CreateMap<ResourceScheduleModel, v0_3_1.ResourceScheduleModel>().ReverseMap();
            CreateMap<v0_3_0.ResourceScheduleModel, v0_3_1.ResourceScheduleModel>().ReverseMap();

            CreateMap<ResourceSettingsModel, v0_3_1.ResourceSettingsModel>().ReverseMap();
            CreateMap<v0_1_0.ResourceSettingsModel, v0_3_1.ResourceSettingsModel>().ReverseMap();

            CreateMap<ProjectPlanModel, v0_3_1.ProjectPlanModel>().ReverseMap();

            #endregion

            #region v0.3.2

            CreateMap<ActivityEdgeModel, v0_3_2.ActivityEdgeModel>().ReverseMap();
            CreateMap<v0_3_0.ActivityEdgeModel, v0_3_2.ActivityEdgeModel>().ReverseMap();

            CreateMap<ActivityModel, v0_3_2.ActivityModel>().ReverseMap();
            CreateMap<v0_3_0.ActivityModel, v0_3_2.ActivityModel>().ReverseMap();

            CreateMap<ActivityNodeModel, v0_3_2.ActivityNodeModel>().ReverseMap();
            CreateMap<v0_3_0.ActivityNodeModel, v0_3_2.ActivityNodeModel>().ReverseMap();

            CreateMap<ArrowGraphModel, v0_3_2.ArrowGraphModel>().ReverseMap();
            CreateMap<v0_3_0.ArrowGraphModel, v0_3_2.ArrowGraphModel>().ReverseMap();

            CreateMap<DependentActivityModel, v0_3_2.DependentActivityModel>().ReverseMap();
            CreateMap<v0_3_0.DependentActivityModel, v0_3_2.DependentActivityModel>().ReverseMap();

            CreateMap<GraphCompilationModel, v0_3_2.GraphCompilationModel>().ReverseMap();
            CreateMap<v0_3_1.GraphCompilationModel, v0_3_2.GraphCompilationModel>().ReverseMap();

            CreateMap<ResourceModel, v0_3_2.ResourceModel>().ReverseMap();
            CreateMap<v0_3_1.ResourceModel, v0_3_2.ResourceModel>().ReverseMap();

            CreateMap<ResourceScheduleModel, v0_3_2.ResourceScheduleModel>().ReverseMap();
            CreateMap<v0_3_1.ResourceScheduleModel, v0_3_2.ResourceScheduleModel>().ReverseMap();

            CreateMap<ResourceSettingsModel, v0_3_2.ResourceSettingsModel>().ReverseMap();
            CreateMap<v0_3_1.ResourceSettingsModel, v0_3_2.ResourceSettingsModel>().ReverseMap();

            CreateMap<WorkStreamModel, v0_3_2.WorkStreamModel>().ReverseMap();

            CreateMap<WorkStreamSettingsModel, v0_3_2.WorkStreamSettingsModel>().ReverseMap();
            CreateMap<WorkStreamSettingsModel, v0_3_2.WorkStreamSettingsModel>().ReverseMap();

            CreateMap<ProjectPlanModel, v0_3_2.ProjectPlanModel>().ReverseMap();

            #endregion

            #region v0.4.0

            CreateMap<ActivityEdgeModel, v0_4_0.ActivityEdgeModel>().ReverseMap();
            CreateMap<v0_3_2.ActivityEdgeModel, v0_4_0.ActivityEdgeModel>().ReverseMap();

            CreateMap<ActivityModel, v0_4_0.ActivityModel>().ReverseMap();
            CreateMap<v0_3_2.ActivityModel, v0_4_0.ActivityModel>().ReverseMap();

            CreateMap<ActivityNodeModel, v0_4_0.ActivityNodeModel>().ReverseMap();
            CreateMap<v0_3_2.ActivityNodeModel, v0_4_0.ActivityNodeModel>().ReverseMap();

            CreateMap<ActivityTrackerModel, v0_4_0.ActivityTrackerModel>().ReverseMap();
            CreateMap<v0_3_0.TrackerModel, v0_4_0.ActivityTrackerModel>().ReverseMap();

            CreateMap<DependentActivityModel, v0_4_0.DependentActivityModel>().ReverseMap();
            CreateMap<v0_3_2.DependentActivityModel, v0_4_0.DependentActivityModel>().ReverseMap();

            CreateMap<ArrowGraphModel, v0_4_0.ArrowGraphModel>().ReverseMap();
            CreateMap<v0_3_2.ArrowGraphModel, v0_4_0.ArrowGraphModel>().ReverseMap();

            CreateMap<DependentActivityModel, v0_4_0.DependentActivityModel>().ReverseMap();
            CreateMap<v0_3_2.DependentActivityModel, v0_4_0.DependentActivityModel>().ReverseMap();

            CreateMap<GraphCompilationModel, v0_4_0.GraphCompilationModel>().ReverseMap();
            CreateMap<v0_3_2.GraphCompilationModel, v0_4_0.GraphCompilationModel>().ReverseMap();

            CreateMap<ResourceModel, v0_4_0.ResourceModel>().ReverseMap();
            CreateMap<v0_3_2.ResourceModel, v0_4_0.ResourceModel>().ReverseMap();

            CreateMap<ResourceScheduleModel, v0_4_0.ResourceScheduleModel>().ReverseMap();
            CreateMap<v0_3_2.ResourceScheduleModel, v0_4_0.ResourceScheduleModel>().ReverseMap();

            CreateMap<ResourceSettingsModel, v0_4_0.ResourceSettingsModel>().ReverseMap();
            CreateMap<v0_3_2.ResourceSettingsModel, v0_4_0.ResourceSettingsModel>().ReverseMap();

            CreateMap<ResourceTrackerModel, v0_4_0.ResourceTrackerModel>().ReverseMap();
            CreateMap<ResourceActivityTrackerModel, v0_4_0.ResourceActivityTrackerModel>().ReverseMap();

            CreateMap<DisplaySettingsModel, v0_4_0.DisplaySettingsModel>().ReverseMap();

            CreateMap<ProjectPlanModel, v0_4_0.ProjectPlanModel>().ReverseMap();

            #endregion
        }
    }
}
