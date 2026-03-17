using Riok.Mapperly.Abstractions;
using Zametek.Common.ProjectPlan;

namespace Zametek.Data.ProjectPlan
{
    [Mapper(

    //// Do not throw when a nullable source is mapped into a non-nullable target
    //ThrowOnPropertyMappingNullMismatch = false,
    //// Do not allow null to be assigned to target properties
    //AllowNullPropertyAssignment = false


        //ThrowOnMappingNullMismatch = false,              // for return values
        //ThrowOnPropertyMappingNullMismatch = false//,      // for properties
        ////AllowNullPropertyAssignment = false              // never assign null to target
    )]
    public partial class VersionMapper
    {
        // ---------------------------------------------------------------------
        // v0.1.0 <-> Current
        // ---------------------------------------------------------------------

        public partial v0_1_0.ActivityEdgeModel FromCurrentToV0_1_0(ActivityEdgeModel src);
        public partial ActivityEdgeModel FromV0_1_0ToCurrent(v0_1_0.ActivityEdgeModel src);

        public partial v0_1_0.ActivityModel FromCurrentToV0_1_0(ActivityModel src);
        public DateTime FromCurrentToV0_1_0(DateTimeOffset value)
            => value.LocalDateTime; // or .UtcDateTime / .DateTime depending on your semantics



        public partial ActivityModel FromV0_1_0ToCurrent(v0_1_0.ActivityModel src);

        public partial v0_1_0.ActivityNodeModel FromCurrentToV0_1_0(ActivityNodeModel src);
        public partial ActivityNodeModel FromV0_1_0ToCurrent(v0_1_0.ActivityNodeModel src);

        public partial v0_1_0.ScheduledActivityModel FromCurrentToV0_1_0(ScheduledActivityModel src);
        public partial ScheduledActivityModel FromV0_1_0ToCurrent(v0_1_0.ScheduledActivityModel src);

        public partial v0_1_0.DependentActivityModel FromCurrentToV0_1_0(DependentActivityModel src);
        public partial DependentActivityModel FromV0_1_0ToCurrent(v0_1_0.DependentActivityModel src);

        public partial v0_1_0.ColorFormatModel FromCurrentToV0_1_0(ColorFormatModel src);
        public partial ColorFormatModel FromV0_1_0ToCurrent(v0_1_0.ColorFormatModel src);

        public partial v0_1_0.EdgeDashStyle FromCurrentToV0_1_0(EdgeDashStyle src);
        public partial EdgeDashStyle FromV0_1_0ToCurrent(v0_1_0.EdgeDashStyle src);

        public partial v0_1_0.EdgeType FromCurrentToV0_1_0(EdgeType src);
        public partial EdgeType FromV0_1_0ToCurrent(v0_1_0.EdgeType src);

        public partial v0_1_0.EdgeTypeFormatModel FromCurrentToV0_1_0(EdgeTypeFormatModel src);
        public partial EdgeTypeFormatModel FromV0_1_0ToCurrent(v0_1_0.EdgeTypeFormatModel src);

        public partial v0_1_0.EdgeWeightStyle FromCurrentToV0_1_0(EdgeWeightStyle src);
        public partial EdgeWeightStyle FromV0_1_0ToCurrent(v0_1_0.EdgeWeightStyle src);

        public partial v0_1_0.EventEdgeModel FromCurrentToV0_1_0(EventEdgeModel src);
        public partial EventEdgeModel FromV0_1_0ToCurrent(v0_1_0.EventEdgeModel src);

        public partial v0_1_0.EventModel FromCurrentToV0_1_0(EventModel src);
        public partial EventModel FromV0_1_0ToCurrent(v0_1_0.EventModel src);

        public partial v0_1_0.EventNodeModel FromCurrentToV0_1_0(EventNodeModel src);
        public partial EventNodeModel FromV0_1_0ToCurrent(v0_1_0.EventNodeModel src);

        public partial v0_1_0.ArrowGraphModel FromCurrentToV0_1_0(ArrowGraphModel src);
        public partial ArrowGraphModel FromV0_1_0ToCurrent(v0_1_0.ArrowGraphModel src);

        public partial v0_1_0.VertexGraphModel FromCurrentToV0_1_0(VertexGraphModel src);
        public partial VertexGraphModel FromV0_1_0ToCurrent(v0_1_0.VertexGraphModel src);

        public partial v0_1_0.ResourceModel FromCurrentToV0_1_0(ResourceModel src);
        public partial ResourceModel FromV0_1_0ToCurrent(v0_1_0.ResourceModel src);

        public partial v0_1_0.ResourceScheduleModel FromCurrentToV0_1_0(ResourceScheduleModel src);
        public partial ResourceScheduleModel FromV0_1_0ToCurrent(v0_1_0.ResourceScheduleModel src);

        public partial v0_1_0.ActivitySeverityModel FromCurrentToV0_1_0(ActivitySeverityModel src);
        public partial ActivitySeverityModel FromV0_1_0ToCurrent(v0_1_0.ActivitySeverityModel src);

        public partial v0_1_0.ArrowGraphSettingsModel FromCurrentToV0_1_0(GraphSettingsModel src);
        public partial GraphSettingsModel FromV0_1_0ToCurrent(v0_1_0.ArrowGraphSettingsModel src);

        public partial v0_1_0.ResourceSettingsModel FromCurrentToV0_1_0(ResourceSettingsModel src);
        public partial ResourceSettingsModel FromV0_1_0ToCurrent(v0_1_0.ResourceSettingsModel src);


        // ---------------------------------------------------------------------
        // v0.2.1 <-> Current
        // ---------------------------------------------------------------------

        public partial v0_2_1.ActivityEdgeModel FromCurrentToV0_2_1(ActivityEdgeModel src);
        public partial ActivityEdgeModel FromV0_2_1ToCurrent(v0_2_1.ActivityEdgeModel src);

        public partial v0_2_1.ActivityModel FromCurrentToV0_2_1(ActivityModel src);
        public partial ActivityModel FromV0_2_1ToCurrent(v0_2_1.ActivityModel src);

        public partial v0_2_1.ActivityNodeModel FromCurrentToV0_2_1(ActivityNodeModel src);
        public partial ActivityNodeModel FromV0_2_1ToCurrent(v0_2_1.ActivityNodeModel src);

        public partial v0_2_1.ScheduledActivityModel FromCurrentToV0_2_1(ScheduledActivityModel src);
        public partial ScheduledActivityModel FromV0_2_1ToCurrent(v0_2_1.ScheduledActivityModel src);

        public partial v0_2_1.DependentActivityModel FromCurrentToV0_2_1(DependentActivityModel src);
        public partial DependentActivityModel FromV0_2_1ToCurrent(v0_2_1.DependentActivityModel src);

        public partial v0_2_1.ArrowGraphModel FromCurrentToV0_2_1(ArrowGraphModel src);
        public partial ArrowGraphModel FromV0_2_1ToCurrent(v0_2_1.ArrowGraphModel src);

        public partial v0_2_1.GraphCompilationModel FromCurrentToV0_2_1(GraphCompilationModel src);
        public partial GraphCompilationModel FromV0_2_1ToCurrent(v0_2_1.GraphCompilationModel src);

        public partial v0_2_1.VertexGraphModel FromCurrentToV0_2_1(VertexGraphModel src);
        public partial VertexGraphModel FromV0_2_1ToCurrent(v0_2_1.VertexGraphModel src);

        public partial v0_2_1.ResourceModel FromCurrentToV0_2_1(ResourceModel src);
        public partial ResourceModel FromV0_2_1ToCurrent(v0_2_1.ResourceModel src);

        public partial v0_2_1.ResourceScheduleModel FromCurrentToV0_2_1(ResourceScheduleModel src);
        public partial ResourceScheduleModel FromV0_2_1ToCurrent(v0_2_1.ResourceScheduleModel src);


        // ---------------------------------------------------------------------
        // v0.1.0 <-> v0.2.1
        // ---------------------------------------------------------------------

        public partial v0_2_1.ActivityEdgeModel FromV0_1_0ToV0_2_1(v0_1_0.ActivityEdgeModel src);
        public partial v0_1_0.ActivityEdgeModel FromV0_2_1ToV0_1_0(v0_2_1.ActivityEdgeModel src);

        public partial v0_2_1.ActivityModel FromV0_1_0ToV0_2_1(v0_1_0.ActivityModel src);
        public partial v0_1_0.ActivityModel FromV0_2_1ToV0_1_0(v0_2_1.ActivityModel src);

        public partial v0_2_1.ActivityNodeModel FromV0_1_0ToV0_2_1(v0_1_0.ActivityNodeModel src);
        public partial v0_1_0.ActivityNodeModel FromV0_2_1ToV0_1_0(v0_2_1.ActivityNodeModel src);

        public partial v0_2_1.ScheduledActivityModel FromV0_1_0ToV0_2_1(v0_1_0.ScheduledActivityModel src);
        public partial v0_1_0.ScheduledActivityModel FromV0_2_1ToV0_1_0(v0_2_1.ScheduledActivityModel src);

        public partial v0_2_1.DependentActivityModel FromV0_1_0ToV0_2_1(v0_1_0.DependentActivityModel src);
        public partial v0_1_0.DependentActivityModel FromV0_2_1ToV0_1_0(v0_2_1.DependentActivityModel src);

        public partial v0_2_1.ArrowGraphModel FromV0_1_0ToV0_2_1(v0_1_0.ArrowGraphModel src);
        public partial v0_1_0.ArrowGraphModel FromV0_2_1ToV0_1_0(v0_2_1.ArrowGraphModel src);

        public partial v0_2_1.VertexGraphModel FromV0_1_0ToV0_2_1(v0_1_0.VertexGraphModel src);
        public partial v0_1_0.VertexGraphModel FromV0_2_1ToV0_1_0(v0_2_1.VertexGraphModel src);

        public partial v0_2_1.ResourceModel FromV0_1_0ToV0_2_1(v0_1_0.ResourceModel src);
        public partial v0_1_0.ResourceModel FromV0_2_1ToV0_1_0(v0_2_1.ResourceModel src);

        public partial v0_2_1.ResourceScheduleModel FromV0_1_0ToV0_2_1(v0_1_0.ResourceScheduleModel src);
        public partial v0_1_0.ResourceScheduleModel FromV0_2_1ToV0_1_0(v0_2_1.ResourceScheduleModel src);


        // ---------------------------------------------------------------------
        // v0.3.0 <-> Current
        // ---------------------------------------------------------------------

        public partial v0_3_0.ActivitySeverityModel FromCurrentToV0_3_0(ActivitySeverityModel src);
        public partial ActivitySeverityModel FromV0_3_0ToCurrent(v0_3_0.ActivitySeverityModel src);

        public partial v0_3_0.ActivityEdgeModel FromCurrentToV0_3_0(ActivityEdgeModel src);
        public partial ActivityEdgeModel FromV0_3_0ToCurrent(v0_3_0.ActivityEdgeModel src);

        public partial v0_3_0.ActivityNodeModel FromCurrentToV0_3_0(ActivityNodeModel src);
        public partial ActivityNodeModel FromV0_3_0ToCurrent(v0_3_0.ActivityNodeModel src);

        public partial v0_3_0.ActivityModel FromCurrentToV0_3_0(ActivityModel src);
        public partial ActivityModel FromV0_3_0ToCurrent(v0_3_0.ActivityModel src);

        public partial v0_3_0.TrackerModel FromCurrentToV0_3_0(ActivityTrackerModel src);
        public partial ActivityTrackerModel FromV0_3_0ToCurrent(v0_3_0.TrackerModel src);

        public partial v0_3_0.DependentActivityModel FromCurrentToV0_3_0(DependentActivityModel src);
        public partial DependentActivityModel FromV0_3_0ToCurrent(v0_3_0.DependentActivityModel src);

        public partial v0_3_0.EventNodeModel FromCurrentToV0_3_0(EventNodeModel src);
        public partial EventNodeModel FromV0_3_0ToCurrent(v0_3_0.EventNodeModel src);

        public partial v0_3_0.ArrowGraphModel FromCurrentToV0_3_0(ArrowGraphModel src);
        public partial ArrowGraphModel FromV0_3_0ToCurrent(v0_3_0.ArrowGraphModel src);

        public partial v0_3_0.GraphCompilationErrorModel FromCurrentToV0_3_0(GraphCompilationErrorModel src);
        public partial GraphCompilationErrorModel FromV0_3_0ToCurrent(v0_3_0.GraphCompilationErrorModel src);

        public partial v0_3_0.GraphCompilationModel FromCurrentToV0_3_0(GraphCompilationModel src);
        public partial GraphCompilationModel FromV0_3_0ToCurrent(v0_3_0.GraphCompilationModel src);

        public partial v0_3_0.ResourceModel FromCurrentToV0_3_0(ResourceModel src);
        public partial ResourceModel FromV0_3_0ToCurrent(v0_3_0.ResourceModel src);

        public partial v0_3_0.ResourceScheduleModel FromCurrentToV0_3_0(ResourceScheduleModel src);
        public partial ResourceScheduleModel FromV0_3_0ToCurrent(v0_3_0.ResourceScheduleModel src);

        public partial v0_3_0.ProjectModel FromCurrentToV0_3_0(ProjectModel src);
        public partial ProjectModel FromV0_3_0ToCurrent(v0_3_0.ProjectModel src);


        // ---------------------------------------------------------------------
        // v0.2.1 <-> v0.3.0
        // ---------------------------------------------------------------------

        public partial v0_3_0.ActivityEdgeModel FromV0_2_1ToV0_3_0(v0_2_1.ActivityEdgeModel src);
        public partial v0_2_1.ActivityEdgeModel FromV0_3_0ToV0_2_1(v0_3_0.ActivityEdgeModel src);

        public partial v0_3_0.ActivityNodeModel FromV0_2_1ToV0_3_0(v0_2_1.ActivityNodeModel src);
        public partial v0_2_1.ActivityNodeModel FromV0_3_0ToV0_2_1(v0_3_0.ActivityNodeModel src);

        public partial v0_3_0.ActivityModel FromV0_2_1ToV0_3_0(v0_2_1.ActivityModel src);
        public partial v0_2_1.ActivityModel FromV0_3_0ToV0_2_1(v0_3_0.ActivityModel src);

        public partial v0_3_0.DependentActivityModel FromV0_2_1ToV0_3_0(v0_2_1.DependentActivityModel src);
        public partial v0_2_1.DependentActivityModel FromV0_3_0ToV0_2_1(v0_3_0.DependentActivityModel src);

        public partial v0_3_0.ArrowGraphModel FromV0_2_1ToV0_3_0(v0_2_1.ArrowGraphModel src);
        public partial v0_2_1.ArrowGraphModel FromV0_3_0ToV0_2_1(v0_3_0.ArrowGraphModel src);

        public partial v0_3_0.ResourceModel FromV0_2_1ToV0_3_0(v0_2_1.ResourceModel src);

        public v0_1_0.ColorFormatModel FromV0_2_1ToV0_3_0(v0_1_0.ColorFormatModel? src)
            => src is null ? new v0_1_0.ColorFormatModel() : src;

        public v0_3_0.ResourceModel FromV0_2_1NullableToV0_3_0(v0_2_1.ResourceModel? src)
            => src is null ? new v0_3_0.ResourceModel() : FromV0_2_1ToV0_3_0(src);





        public partial v0_2_1.ResourceModel FromV0_3_0ToV0_2_1(v0_3_0.ResourceModel src);

        //[MapProperty(
        //    nameof(v0_2_1.ResourceScheduleModel.Resource),
        //    nameof(v0_3_0.ResourceScheduleModel.Resource),
        //    Use = nameof(FromV0_2_1NullableToV0_3_0AllowNull))]
        public partial v0_3_0.ResourceScheduleModel FromV0_2_1ToV0_3_0(v0_2_1.ResourceScheduleModel src);

        //public v0_3_0.ResourceModel? FromV0_2_1NullableToV0_3_0AllowNull(v0_2_1.ResourceModel? src)
        //    => src is null ? null : FromV0_2_1NullableToV0_3_0(src); // allow null pass-through here






        public partial v0_2_1.ResourceScheduleModel FromV0_3_0ToV0_2_1(v0_3_0.ResourceScheduleModel src);


        // ---------------------------------------------------------------------
        // v0.3.1 <-> Current and v0.3.0
        // ---------------------------------------------------------------------

        public partial v0_3_1.GraphCompilationModel FromCurrentToV0_3_1(GraphCompilationModel src);
        public partial GraphCompilationModel FromV0_3_1ToCurrent(v0_3_1.GraphCompilationModel src);

        public partial v0_3_1.GraphCompilationModel FromV0_3_0ToV0_3_1(v0_3_0.GraphCompilationModel src);
        public partial v0_3_0.GraphCompilationModel FromV0_3_1ToV0_3_0(v0_3_1.GraphCompilationModel src);

        public partial v0_3_1.ResourceModel FromCurrentToV0_3_1(ResourceModel src);
        public partial ResourceModel FromV0_3_1ToCurrent(v0_3_1.ResourceModel src);

        public partial v0_3_1.ResourceModel FromV0_1_0ToV0_3_1(v0_1_0.ResourceModel src);
        public partial v0_1_0.ResourceModel FromV0_3_1ToV0_1_0(v0_3_1.ResourceModel src);

        public partial v0_3_1.ResourceModel FromV0_3_0ToV0_3_1(v0_3_0.ResourceModel src);
        public partial v0_3_0.ResourceModel FromV0_3_1ToV0_3_0(v0_3_1.ResourceModel src);

        public partial v0_3_1.ResourceScheduleModel FromCurrentToV0_3_1(ResourceScheduleModel src);
        public partial ResourceScheduleModel FromV0_3_1ToCurrent(v0_3_1.ResourceScheduleModel src);

        public partial v0_3_1.ResourceScheduleModel FromV0_3_0ToV0_3_1(v0_3_0.ResourceScheduleModel src);
        public partial v0_3_0.ResourceScheduleModel FromV0_3_1ToV0_3_0(v0_3_1.ResourceScheduleModel src);

        public partial v0_3_1.ResourceSettingsModel FromCurrentToV0_3_1(ResourceSettingsModel src);
        public partial ResourceSettingsModel FromV0_3_1ToCurrent(v0_3_1.ResourceSettingsModel src);

        public partial v0_3_1.ResourceSettingsModel FromV0_1_0ToV0_3_1(v0_1_0.ResourceSettingsModel src);
        public partial v0_1_0.ResourceSettingsModel FromV0_3_1ToV0_1_0(v0_3_1.ResourceSettingsModel src);

        public partial v0_3_1.ProjectModel FromCurrentToV0_3_1(ProjectModel src);
        public partial ProjectModel FromV0_3_1ToCurrent(v0_3_1.ProjectModel src);


        // ---------------------------------------------------------------------
        // v0.3.2 <-> Current and v0.3.0/0.3.1
        // ---------------------------------------------------------------------

        public partial v0_3_2.ActivityEdgeModel FromCurrentToV0_3_2(ActivityEdgeModel src);
        public partial ActivityEdgeModel FromV0_3_2ToCurrent(v0_3_2.ActivityEdgeModel src);

        public partial v0_3_2.ActivityEdgeModel FromV0_3_0ToV0_3_2(v0_3_0.ActivityEdgeModel src);
        public partial v0_3_0.ActivityEdgeModel FromV0_3_2ToV0_3_0(v0_3_2.ActivityEdgeModel src);

        public partial v0_3_2.ActivityModel FromCurrentToV0_3_2(ActivityModel src);
        public partial ActivityModel FromV0_3_2ToCurrent(v0_3_2.ActivityModel src);

        public partial v0_3_2.ActivityModel FromV0_3_0ToV0_3_2(v0_3_0.ActivityModel src);
        public partial v0_3_0.ActivityModel FromV0_3_2ToV0_3_0(v0_3_2.ActivityModel src);

        public partial v0_3_2.ActivityNodeModel FromCurrentToV0_3_2(ActivityNodeModel src);
        public partial ActivityNodeModel FromV0_3_2ToCurrent(v0_3_2.ActivityNodeModel src);

        public partial v0_3_2.ActivityNodeModel FromV0_3_0ToV0_3_2(v0_3_0.ActivityNodeModel src);
        public partial v0_3_0.ActivityNodeModel FromV0_3_2ToV0_3_0(v0_3_2.ActivityNodeModel src);

        public partial v0_3_2.ArrowGraphModel FromCurrentToV0_3_2(ArrowGraphModel src);
        public partial ArrowGraphModel FromV0_3_2ToCurrent(v0_3_2.ArrowGraphModel src);

        public partial v0_3_2.ArrowGraphModel FromV0_3_0ToV0_3_2(v0_3_0.ArrowGraphModel src);
        public partial v0_3_0.ArrowGraphModel FromV0_3_2ToV0_3_0(v0_3_2.ArrowGraphModel src);

        public partial v0_3_2.DependentActivityModel FromCurrentToV0_3_2(DependentActivityModel src);
        public partial DependentActivityModel FromV0_3_2ToCurrent(v0_3_2.DependentActivityModel src);

        public partial v0_3_2.DependentActivityModel FromV0_3_0ToV0_3_2(v0_3_0.DependentActivityModel src);
        public partial v0_3_0.DependentActivityModel FromV0_3_2ToV0_3_0(v0_3_2.DependentActivityModel src);

        public partial v0_3_2.GraphCompilationModel FromCurrentToV0_3_2(GraphCompilationModel src);
        public partial GraphCompilationModel FromV0_3_2ToCurrent(v0_3_2.GraphCompilationModel src);

        public partial v0_3_2.GraphCompilationModel FromV0_3_1ToV0_3_2(v0_3_1.GraphCompilationModel src);
        public partial v0_3_1.GraphCompilationModel FromV0_3_2ToV0_3_1(v0_3_2.GraphCompilationModel src);

        public partial v0_3_2.ResourceModel FromCurrentToV0_3_2(ResourceModel src);
        public partial ResourceModel FromV0_3_2ToCurrent(v0_3_2.ResourceModel src);

        public partial v0_3_2.ResourceModel FromV0_3_1ToV0_3_2(v0_3_1.ResourceModel src);
        public partial v0_3_1.ResourceModel FromV0_3_2ToV0_3_1(v0_3_2.ResourceModel src);

        public partial v0_3_2.ResourceScheduleModel FromCurrentToV0_3_2(ResourceScheduleModel src);
        public partial ResourceScheduleModel FromV0_3_2ToCurrent(v0_3_2.ResourceScheduleModel src);

        public partial v0_3_2.ResourceScheduleModel FromV0_3_1ToV0_3_2(v0_3_1.ResourceScheduleModel src);
        public partial v0_3_1.ResourceScheduleModel FromV0_3_2ToV0_3_1(v0_3_2.ResourceScheduleModel src);

        public partial v0_3_2.ResourceSettingsModel FromCurrentToV0_3_2(ResourceSettingsModel src);
        public partial ResourceSettingsModel FromV0_3_2ToCurrent(v0_3_2.ResourceSettingsModel src);

        public partial v0_3_2.ResourceSettingsModel FromV0_3_1ToV0_3_2(v0_3_1.ResourceSettingsModel src);
        public partial v0_3_1.ResourceSettingsModel FromV0_3_2ToV0_3_1(v0_3_2.ResourceSettingsModel src);

        public partial v0_3_2.WorkStreamModel FromCurrentToV0_3_2(WorkStreamModel src);
        public partial WorkStreamModel FromV0_3_2ToCurrent(v0_3_2.WorkStreamModel src);

        public partial v0_3_2.WorkStreamSettingsModel FromCurrentToV0_3_2(WorkStreamSettingsModel src);
        public partial WorkStreamSettingsModel FromV0_3_2ToCurrent(v0_3_2.WorkStreamSettingsModel src);

        public partial v0_3_2.ProjectModel FromCurrentToV0_3_2(ProjectModel src);
        public partial ProjectModel FromV0_3_2ToCurrent(v0_3_2.ProjectModel src);


        // ---------------------------------------------------------------------
        // v0.4.0 <-> Current and v0.3.2
        // ---------------------------------------------------------------------

        public partial v0_4_0.ActivityEdgeModel FromCurrentToV0_4_0(ActivityEdgeModel src);
        public partial ActivityEdgeModel FromV0_4_0ToCurrent(v0_4_0.ActivityEdgeModel src);

        public partial v0_4_0.ActivityEdgeModel FromV0_3_2ToV0_4_0(v0_3_2.ActivityEdgeModel src);
        public partial v0_3_2.ActivityEdgeModel FromV0_4_0ToV0_3_2(v0_4_0.ActivityEdgeModel src);

        public partial v0_4_0.ActivityModel FromCurrentToV0_4_0(ActivityModel src);
        public partial ActivityModel FromV0_4_0ToCurrent(v0_4_0.ActivityModel src);

        public partial v0_4_0.ActivityModel FromV0_3_2ToV0_4_0(v0_3_2.ActivityModel src);
        public partial v0_3_2.ActivityModel FromV0_4_0ToV0_3_2(v0_4_0.ActivityModel src);

        public partial v0_4_0.ActivityNodeModel FromCurrentToV0_4_0(ActivityNodeModel src);
        public partial ActivityNodeModel FromV0_4_0ToCurrent(v0_4_0.ActivityNodeModel src);

        public partial v0_4_0.ActivityNodeModel FromV0_3_2ToV0_4_0(v0_3_2.ActivityNodeModel src);
        public partial v0_3_2.ActivityNodeModel FromV0_4_0ToV0_3_2(v0_4_0.ActivityNodeModel src);

        public partial v0_4_0.ActivityTrackerModel FromCurrentToV0_4_0(ActivityTrackerModel src);
        public partial ActivityTrackerModel FromV0_4_0ToCurrent(v0_4_0.ActivityTrackerModel src);

        public partial v0_4_0.ActivityTrackerModel FromV0_3_0ToV0_4_0(v0_3_0.TrackerModel src);
        public partial v0_3_0.TrackerModel FromV0_4_0ToV0_3_0(v0_4_0.ActivityTrackerModel src);

        public partial v0_4_0.DependentActivityModel FromCurrentToV0_4_0(DependentActivityModel src);
        public partial DependentActivityModel FromV0_4_0ToCurrent(v0_4_0.DependentActivityModel src);

        public partial v0_4_0.DependentActivityModel FromV0_3_2ToV0_4_0(v0_3_2.DependentActivityModel src);
        public partial v0_3_2.DependentActivityModel FromV0_4_0ToV0_3_2(v0_4_0.DependentActivityModel src);

        public partial v0_4_0.ScheduledActivityModel FromCurrentToV0_4_0(ScheduledActivityModel src);
        public partial ScheduledActivityModel FromV0_4_0ToCurrent(v0_4_0.ScheduledActivityModel src);

        public partial v0_4_0.ScheduledActivityModel FromV0_2_1ToV0_4_0(v0_2_1.ScheduledActivityModel src);
        public partial v0_2_1.ScheduledActivityModel FromV0_4_0ToV0_2_1(v0_4_0.ScheduledActivityModel src);

        public partial v0_4_0.ArrowGraphModel FromCurrentToV0_4_0(ArrowGraphModel src);
        public partial ArrowGraphModel FromV0_4_0ToCurrent(v0_4_0.ArrowGraphModel src);

        public partial v0_4_0.ArrowGraphModel FromV0_3_2ToV0_4_0(v0_3_2.ArrowGraphModel src);
        public partial v0_3_2.ArrowGraphModel FromV0_4_0ToV0_3_2(v0_4_0.ArrowGraphModel src);

        public partial v0_4_0.GraphCompilationModel FromCurrentToV0_4_0(GraphCompilationModel src);
        public partial GraphCompilationModel FromV0_4_0ToCurrent(v0_4_0.GraphCompilationModel src);

        public partial v0_4_0.GraphCompilationModel FromV0_3_2ToV0_4_0(v0_3_2.GraphCompilationModel src);
        public partial v0_3_2.GraphCompilationModel FromV0_4_0ToV0_3_2(v0_4_0.GraphCompilationModel src);

        public partial v0_4_0.ResourceModel FromCurrentToV0_4_0(ResourceModel src);
        public partial ResourceModel FromV0_4_0ToCurrent(v0_4_0.ResourceModel src);

        public partial v0_4_0.ResourceModel FromV0_3_2ToV0_4_0(v0_3_2.ResourceModel src);
        public partial v0_3_2.ResourceModel FromV0_4_0ToV0_3_2(v0_4_0.ResourceModel src);

        public partial v0_4_0.ResourceScheduleModel FromCurrentToV0_4_0(ResourceScheduleModel src);
        public partial ResourceScheduleModel FromV0_4_0ToCurrent(v0_4_0.ResourceScheduleModel src);

        public partial v0_4_0.ResourceScheduleModel FromV0_3_2ToV0_4_0(v0_3_2.ResourceScheduleModel src);
        public partial v0_3_2.ResourceScheduleModel FromV0_4_0ToV0_3_2(v0_4_0.ResourceScheduleModel src);

        public partial v0_4_0.ResourceSettingsModel FromCurrentToV0_4_0(ResourceSettingsModel src);
        public partial ResourceSettingsModel FromV0_4_0ToCurrent(v0_4_0.ResourceSettingsModel src);

        public partial v0_4_0.ResourceSettingsModel FromV0_3_2ToV0_4_0(v0_3_2.ResourceSettingsModel src);
        public partial v0_3_2.ResourceSettingsModel FromV0_4_0ToV0_3_2(v0_4_0.ResourceSettingsModel src);

        public partial v0_4_0.ResourceTrackerModel FromCurrentToV0_4_0(ResourceTrackerModel src);
        public partial ResourceTrackerModel FromV0_4_0ToCurrent(v0_4_0.ResourceTrackerModel src);

        public partial v0_4_0.ResourceActivityTrackerModel FromCurrentToV0_4_0(ResourceActivityTrackerModel src);
        public partial ResourceActivityTrackerModel FromV0_4_0ToCurrent(v0_4_0.ResourceActivityTrackerModel src);

        public partial v0_4_0.DisplaySettingsModel FromCurrentToV0_4_0(DisplaySettingsModel src);
        public partial DisplaySettingsModel FromV0_4_0ToCurrent(v0_4_0.DisplaySettingsModel src);

        public partial v0_4_0.ProjectModel FromCurrentToV0_4_0(ProjectModel src);
        public partial ProjectModel FromV0_4_0ToCurrent(v0_4_0.ProjectModel src);


        // ---------------------------------------------------------------------
        // v0.4.1 <-> Current and v0.4.0
        // ---------------------------------------------------------------------

        public partial v0_4_1.DisplaySettingsModel FromCurrentToV0_4_1(DisplaySettingsModel src);
        public partial DisplaySettingsModel FromV0_4_1ToCurrent(v0_4_1.DisplaySettingsModel src);

        public partial v0_4_1.DisplaySettingsModel FromV0_4_0ToV0_4_1(v0_4_0.DisplaySettingsModel src);
        public partial v0_4_0.DisplaySettingsModel FromV0_4_1ToV0_4_0(v0_4_1.DisplaySettingsModel src);

        public partial v0_4_1.ProjectModel FromCurrentToV0_4_1(ProjectModel src);
        public partial ProjectModel FromV0_4_1ToCurrent(v0_4_1.ProjectModel src);

        public partial v0_4_1.ProjectModel FromV0_4_0ToV0_4_1(v0_4_0.ProjectModel src);
        public partial v0_4_0.ProjectModel FromV0_4_1ToV0_4_0(v0_4_1.ProjectModel src);

        public partial v0_4_1.AppSettingsModel FromCurrentToV0_4_1(AppSettingsModel src);
        public partial AppSettingsModel FromV0_4_1ToCurrent(v0_4_1.AppSettingsModel src);


        // ---------------------------------------------------------------------
        // v0.4.2 <-> Current and v0.4.0/0.4.1
        // ---------------------------------------------------------------------

        public partial v0_4_2.DependentActivityModel FromCurrentToV0_4_2(DependentActivityModel src);
        public partial DependentActivityModel FromV0_4_2ToCurrent(v0_4_2.DependentActivityModel src);

        public partial v0_4_2.DependentActivityModel FromV0_4_0ToV0_4_2(v0_4_0.DependentActivityModel src);
        public partial v0_4_0.DependentActivityModel FromV0_4_2ToV0_4_0(v0_4_2.DependentActivityModel src);

        public partial v0_4_2.GraphCompilationModel FromCurrentToV0_4_2(GraphCompilationModel src);
        public partial GraphCompilationModel FromV0_4_2ToCurrent(v0_4_2.GraphCompilationModel src);

        public partial v0_4_2.GraphCompilationModel FromV0_4_0ToV0_4_2(v0_4_0.GraphCompilationModel src);
        public partial v0_4_0.GraphCompilationModel FromV0_4_2ToV0_4_0(v0_4_2.GraphCompilationModel src);

        public partial v0_4_2.ProjectModel FromCurrentToV0_4_2(ProjectModel src);
        public partial ProjectModel FromV0_4_2ToCurrent(v0_4_2.ProjectModel src);

        public partial v0_4_2.ProjectModel FromV0_4_1ToV0_4_2(v0_4_1.ProjectModel src);
        public partial v0_4_1.ProjectModel FromV0_4_2ToV0_4_1(v0_4_2.ProjectModel src);


        // ---------------------------------------------------------------------
        // v0.4.3 <-> Current and v0.4.2/0.4.0
        // ---------------------------------------------------------------------

        public partial v0_4_3.DependentActivityModel FromCurrentToV0_4_3(DependentActivityModel src);
        public partial DependentActivityModel FromV0_4_3ToCurrent(v0_4_3.DependentActivityModel src);

        public partial v0_4_3.DependentActivityModel FromV0_4_2ToV0_4_3(v0_4_2.DependentActivityModel src);
        public partial v0_4_2.DependentActivityModel FromV0_4_3ToV0_4_2(v0_4_3.DependentActivityModel src);

        public partial v0_4_3.GraphCompilationModel FromCurrentToV0_4_3(GraphCompilationModel src);
        public partial GraphCompilationModel FromV0_4_3ToCurrent(v0_4_3.GraphCompilationModel src);

        public partial v0_4_3.GraphCompilationModel FromV0_4_2ToV0_4_3(v0_4_2.GraphCompilationModel src);
        public partial v0_4_2.GraphCompilationModel FromV0_4_3ToV0_4_2(v0_4_3.GraphCompilationModel src);

        public partial v0_4_3.ResourceScheduleModel FromCurrentToV0_4_3(ResourceScheduleModel src);
        public partial ResourceScheduleModel FromV0_4_3ToCurrent(v0_4_3.ResourceScheduleModel src);

        public partial v0_4_3.ResourceScheduleModel FromV0_4_0ToV0_4_3(v0_4_0.ResourceScheduleModel src);
        public partial v0_4_0.ResourceScheduleModel FromV0_4_3ToV0_4_0(v0_4_3.ResourceScheduleModel src);

        public partial v0_4_3.ProjectModel FromCurrentToV0_4_3(ProjectModel src);
        public partial ProjectModel FromV0_4_3ToCurrent(v0_4_3.ProjectModel src);

        public partial v0_4_3.ProjectModel FromV0_4_2ToV0_4_3(v0_4_2.ProjectModel src);
        public partial v0_4_2.ProjectModel FromV0_4_3ToV0_4_2(v0_4_3.ProjectModel src);


        // ---------------------------------------------------------------------
        // v0.4.4 <-> Current and v0.4.0/0.4.1/0.4.3
        // ---------------------------------------------------------------------

        public partial v0_4_4.ActivityModel FromCurrentToV0_4_4(ActivityModel src);
        public partial ActivityModel FromV0_4_4ToCurrent(v0_4_4.ActivityModel src);


        public string FromV0_4_4ToCurrent(string? src)
            => src is null ? string.Empty : src;




        public partial v0_4_4.ActivityModel FromV0_4_0ToV0_4_4(v0_4_0.ActivityModel src);
        public partial v0_4_0.ActivityModel FromV0_4_4ToV0_4_0(v0_4_4.ActivityModel src);

        public partial v0_4_4.ScheduledActivityModel FromCurrentToV0_4_4(ScheduledActivityModel src);
        public partial ScheduledActivityModel FromV0_4_4ToCurrent(v0_4_4.ScheduledActivityModel src);

        public partial v0_4_4.ScheduledActivityModel FromV0_4_0ToV0_4_4(v0_4_0.ScheduledActivityModel src);
        public partial v0_4_0.ScheduledActivityModel FromV0_4_4ToV0_4_0(v0_4_4.ScheduledActivityModel src);

        public partial v0_4_4.DependentActivityModel FromCurrentToV0_4_4(DependentActivityModel src);
        public partial DependentActivityModel FromV0_4_4ToCurrent(v0_4_4.DependentActivityModel src);

        public partial v0_4_4.DependentActivityModel FromV0_4_3ToV0_4_4(v0_4_3.DependentActivityModel src);
        public partial v0_4_3.DependentActivityModel FromV0_4_4ToV0_4_3(v0_4_4.DependentActivityModel src);

        public partial v0_4_4.GraphCompilationModel FromCurrentToV0_4_4(GraphCompilationModel src);
        public partial GraphCompilationModel FromV0_4_4ToCurrent(v0_4_4.GraphCompilationModel src);

        public partial v0_4_4.GraphCompilationModel FromV0_4_3ToV0_4_4(v0_4_3.GraphCompilationModel src);
        public partial v0_4_3.GraphCompilationModel FromV0_4_4ToV0_4_3(v0_4_4.GraphCompilationModel src);

        public partial v0_4_4.ResourceModel FromCurrentToV0_4_4(ResourceModel src);
        public partial ResourceModel FromV0_4_4ToCurrent(v0_4_4.ResourceModel src);

        public partial v0_4_4.ResourceModel FromV0_4_0ToV0_4_4(v0_4_0.ResourceModel src);
        public partial v0_4_0.ResourceModel FromV0_4_4ToV0_4_0(v0_4_4.ResourceModel src);

        public partial v0_4_4.ResourceScheduleModel FromCurrentToV0_4_4(ResourceScheduleModel src);
        public partial ResourceScheduleModel FromV0_4_4ToCurrent(v0_4_4.ResourceScheduleModel src);

        public partial v0_4_4.ResourceScheduleModel FromV0_4_3ToV0_4_4(v0_4_3.ResourceScheduleModel src);
        public partial v0_4_3.ResourceScheduleModel FromV0_4_4ToV0_4_3(v0_4_4.ResourceScheduleModel src);

        public partial v0_4_4.AppSettingsModel FromCurrentToV0_4_4(AppSettingsModel src);
        public partial AppSettingsModel FromV0_4_4ToCurrent(v0_4_4.AppSettingsModel src);

        public partial v0_4_4.AppSettingsModel FromV0_4_1ToV0_4_4(v0_4_1.AppSettingsModel src);
        public partial v0_4_1.AppSettingsModel FromV0_4_4ToV0_4_1(v0_4_4.AppSettingsModel src);

        public partial v0_4_4.DisplaySettingsModel FromCurrentToV0_4_4(DisplaySettingsModel src);
        public partial DisplaySettingsModel FromV0_4_4ToCurrent(v0_4_4.DisplaySettingsModel src);

        public partial v0_4_4.DisplaySettingsModel FromV0_4_1ToV0_4_4(v0_4_1.DisplaySettingsModel src);
        public partial v0_4_1.DisplaySettingsModel FromV0_4_4ToV0_4_1(v0_4_4.DisplaySettingsModel src);

        public partial v0_4_4.ResourceSettingsModel FromCurrentToV0_4_4(ResourceSettingsModel src);
        public partial ResourceSettingsModel FromV0_4_4ToCurrent(v0_4_4.ResourceSettingsModel src);

        public partial v0_4_4.ResourceSettingsModel FromV0_4_0ToV0_4_4(v0_4_0.ResourceSettingsModel src);
        public partial v0_4_0.ResourceSettingsModel FromV0_4_4ToV0_4_0(v0_4_4.ResourceSettingsModel src);

        public partial v0_4_4.ProjectModel FromCurrentToV0_4_4(ProjectModel src);
        public partial ProjectModel FromV0_4_4ToCurrent(v0_4_4.ProjectModel src);

        public partial v0_4_4.ProjectModel FromV0_4_3ToV0_4_4(v0_4_3.ProjectModel src);
        public partial v0_4_3.ProjectModel FromV0_4_4ToV0_4_3(v0_4_4.ProjectModel src);


        // ---------------------------------------------------------------------
        // v0.5.0 <-> Current and v0.4.4
        // ---------------------------------------------------------------------

        public partial v0_5_0.NodeBorderDashStyle FromCurrentToV0_5_0(NodeBorderDashStyle src);
        public partial NodeBorderDashStyle FromV0_5_0ToCurrent(v0_5_0.NodeBorderDashStyle src);

        public partial v0_5_0.NodeType FromCurrentToV0_5_0(NodeType src);
        public partial NodeType FromV0_5_0ToCurrent(v0_5_0.NodeType src);

        public partial v0_5_0.NodeTypeFormatModel FromCurrentToV0_5_0(NodeTypeFormatModel src);
        public partial NodeTypeFormatModel FromV0_5_0ToCurrent(v0_5_0.NodeTypeFormatModel src);

        public partial v0_5_0.NodeBorderWeightStyle FromCurrentToV0_5_0(NodeBorderWeightStyle src);
        public partial NodeBorderWeightStyle FromV0_5_0ToCurrent(v0_5_0.NodeBorderWeightStyle src);

        public partial v0_5_0.GraphSettingsModel FromCurrentToV0_5_0(GraphSettingsModel src);
        public partial GraphSettingsModel FromV0_5_0ToCurrent(v0_5_0.GraphSettingsModel src);

        public partial v0_5_0.DisplaySettingsModel FromCurrentToV0_5_0(DisplaySettingsModel src);
        public partial DisplaySettingsModel FromV0_5_0ToCurrent(v0_5_0.DisplaySettingsModel src);

        public partial v0_5_0.DisplaySettingsModel FromV0_4_4ToV0_5_0(v0_4_4.DisplaySettingsModel src);
        public partial v0_4_4.DisplaySettingsModel FromV0_5_0ToV0_4_4(v0_5_0.DisplaySettingsModel src);

        public partial v0_5_0.ProjectModel FromCurrentToV0_5_0(ProjectModel src);
        public partial ProjectModel FromV0_5_0ToCurrent(v0_5_0.ProjectModel src);

        public partial v0_5_0.ProjectModel FromV0_4_4ToV0_5_0(v0_4_4.ProjectModel src);
        public partial v0_4_4.ProjectModel FromV0_5_0ToV0_4_4(v0_5_0.ProjectModel src);

        public partial v0_5_0.MetricsModel FromCurrentToV0_5_0(MetricsModel src);
        public partial MetricsModel FromV0_5_0ToCurrent(v0_5_0.MetricsModel src);

        public partial v0_5_0.RisksModel FromCurrentToV0_5_0(RisksModel src);
        public partial RisksModel FromV0_5_0ToCurrent(v0_5_0.RisksModel src);

        public partial v0_5_0.CostsModel FromCurrentToV0_5_0(CostsModel src);
        public partial CostsModel FromV0_5_0ToCurrent(v0_5_0.CostsModel src);

        public partial v0_5_0.BillingsModel FromCurrentToV0_5_0(BillingsModel src);
        public partial BillingsModel FromV0_5_0ToCurrent(v0_5_0.BillingsModel src);

        public partial v0_5_0.MarginsModel FromCurrentToV0_5_0(MarginsModel src);
        public partial MarginsModel FromV0_5_0ToCurrent(v0_5_0.MarginsModel src);

        public partial v0_5_0.EffortsModel FromCurrentToV0_5_0(EffortsModel src);
        public partial EffortsModel FromV0_5_0ToCurrent(v0_5_0.EffortsModel src);

        public partial v0_5_0.NetworkModel FromCurrentToV0_5_0(NetworkModel src);
        public partial NetworkModel FromV0_5_0ToCurrent(v0_5_0.NetworkModel src);


        // ---------------------------------------------------------------------
        // v0.6.0 <-> Current and v0.5.0 / v0.4.4
        // ---------------------------------------------------------------------

        public partial v0_6_0.ProjectScenarioModel FromV0_5_0ToV0_6_0(v0_5_0.ProjectModel src);
        public partial v0_5_0.ProjectModel FromV0_6_0ToV0_5_0(v0_6_0.ProjectScenarioModel src);

        public partial v0_6_0.ProjectModel FromCurrentToV0_6_0(ProjectModel src);
        public partial ProjectModel FromV0_6_0ToCurrent(v0_6_0.ProjectModel src);

        public partial v0_6_0.ProjectScenarioModel FromCurrentToV0_6_0(ProjectScenarioModel src);
        public partial ProjectScenarioModel FromV0_6_0ToCurrent(v0_6_0.ProjectScenarioModel src);

        public partial v0_6_0.ProjectScenarioNodeModel FromCurrentToV0_6_0(ProjectScenarioNodeModel src);
        public partial ProjectScenarioNodeModel FromV0_6_0ToCurrent(v0_6_0.ProjectScenarioNodeModel src);

        public partial v0_6_0.ProjectScenarioFileModel FromCurrentToV0_6_0(ProjectScenarioFileModel src);
        public partial ProjectScenarioFileModel FromV0_6_0ToCurrent(v0_6_0.ProjectScenarioFileModel src);

        public partial v0_6_0.ProjectScenarioTagModel FromCurrentToV0_6_0(ProjectScenarioTagModel src);
        public partial ProjectScenarioTagModel FromV0_6_0ToCurrent(v0_6_0.ProjectScenarioTagModel src);

        public partial v0_6_0.AppSettingsModel FromCurrentToV0_6_0(AppSettingsModel src);
        public partial AppSettingsModel FromV0_6_0ToCurrent(v0_6_0.AppSettingsModel src);

        public partial v0_6_0.AppSettingsModel FromV0_4_4ToV0_6_0(v0_4_4.AppSettingsModel src);
        public partial v0_4_4.AppSettingsModel FromV0_6_0ToV0_4_4(v0_6_0.AppSettingsModel src);
    }




}
