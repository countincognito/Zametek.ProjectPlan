using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Builds the outputs of a compilation: the two graphs, the two series sets and the metrics. Each build reads
    /// only what it is given, and it draws colours from a sequence that it owns. So a build needs no lock, and two
    /// builds can run at the same time.
    /// </summary>
    internal sealed class CompilationOutputService
    {
        #region Fields

        /// <summary>
        /// The colours of the series that the Build methods of the core give, one after the other. The series of the
        /// resources start the sequence again, and the series of the tracking carry on where those stopped.
        /// </summary>
        /// <remarks>
        /// A cascade does not use this sequence. It makes one of its own for its two series (<see cref="Compute"/>),
        /// and so a cascade and these methods never shift the colours of one another. The core calls these methods
        /// under its lock, and so they run one at a time.
        /// </remarks>
        private readonly PresetColorSequence m_BuildColors = new();

        private readonly IGraphCompilationService m_GraphCompilationService;
        private readonly IResourceSchedulingService m_ResourceSchedulingService;
        private readonly IMetricCalculationService m_MetricCalculationService;

        #endregion

        #region Ctors

        public CompilationOutputService(
            IGraphCompilationService graphCompilationService,
            IResourceSchedulingService resourceSchedulingService,
            IMetricCalculationService metricCalculationService)
        {
            ArgumentNullException.ThrowIfNull(graphCompilationService);
            ArgumentNullException.ThrowIfNull(resourceSchedulingService);
            ArgumentNullException.ThrowIfNull(metricCalculationService);
            m_GraphCompilationService = graphCompilationService;
            m_ResourceSchedulingService = resourceSchedulingService;
            m_MetricCalculationService = metricCalculationService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Builds the outputs of a cascade, in dependency order, from a copy of what they read. Needs no lock of
        /// the core and no lock of its own. The cascade makes a sequence of colours for its two series, and the series
        /// of the tracking carry on where the series of the resources stopped.
        /// </summary>
        public CascadeOutputs Compute(CascadeInputs inputs)
        {
            var presetColors = new PresetColorSequence();

            ArrowGraphModel arrowGraph = ComputeArrowGraph(
                inputs.GraphCompilation,
                inputs.HasCompilationErrors);

            VertexGraphModel vertexGraph = ComputeVertexGraph(
                inputs.GraphCompilation,
                inputs.HasCompilationErrors,
                inputs.ResourceSettings,
                inputs.WorkStreamSettings);

            ResourceSeriesSetModel resourceSeriesSet = ComputeResourceSeriesSet(
                inputs.GraphCompilation,
                inputs.ResourceSettings,
                presetColors);

            TrackingSeriesSetModel trackingSeriesSet = ComputeTrackingSeriesSet(
                inputs.ActivityModels,
                inputs.ResourceSettings,
                inputs.HasResources,
                presetColors);

            NetworkModel networkMetrics = ComputeNetworkMetrics(
                inputs.GraphCompilation,
                inputs.HasCompilationErrors,
                inputs.ProjectStart,
                inputs.StartTime,
                inputs.FinishTime);

            RisksModel riskMetrics = ComputeRiskMetrics(
                inputs.GraphCompilation,
                inputs.HasCompilationErrors,
                inputs.ActivitySeverities);

            (CostsModel costs, BillingsModel billings, MarginsModel margins, EffortsModel efforts, List<ResourceMetricsModel> resourceMetrics) =
                ComputeFinancialMetrics(resourceSeriesSet, inputs.HasCompilationErrors);

            return new CascadeOutputs(
                arrowGraph,
                vertexGraph,
                resourceSeriesSet,
                trackingSeriesSet,
                networkMetrics,
                riskMetrics,
                costs,
                billings,
                margins,
                efforts,
                resourceMetrics);
        }

        // The seven outputs of the cascade, each built from the values that it reads and nothing else. The Build*
        // methods of the core give them the live values, under its lock; the cascade gives them a copy, without it.
        // Either way the output is the same.

        public ArrowGraphModel ComputeArrowGraph(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors)
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(CoreViewModel.BuildArrowGraph)}");

            if (hasCompilationErrors)
            {
                return new ArrowGraphModel();
            }

            // Stamp each event with a stable, activity-anchored id (ArrowEventIdMapper), so the
            // interactive layout can persist and rehydrate by it; the compiler's own event ids
            // are transient (regenerated every compile) and so cannot key a saved arrangement.
            var arrowGraph = m_GraphCompilationService.BuildArrowGraph(
                graphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject()));

            return ArrowEventIdMapper.ApplyStableIds(arrowGraph);
        }

        public VertexGraphModel ComputeVertexGraph(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors,
            ResourceSettingsModel resourceSettings,
            WorkStreamSettingsModel workStreamSettings)
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(CoreViewModel.BuildVertexGraph)}");

            if (hasCompilationErrors)
            {
                return new VertexGraphModel();
            }

            return m_GraphCompilationService.BuildVertexGraph(
                graphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject()),
                resourceSettings.Resources,
                resourceSettings.AreDisabled,
                workStreamSettings.WorkStreams);
        }

        // The series of the resources and of the tracking draw colours for the resources that have none, and the second
        // carries on where the first stopped. So the two take the same sequence. The cascade gives them one of its own,
        // and the Build methods of the core give them m_BuildColors.

        public ResourceSeriesSetModel ComputeResourceSeriesSet(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            ResourceSettingsModel resourceSettings) =>
            ComputeResourceSeriesSet(graphCompilation, resourceSettings, m_BuildColors);

        public ResourceSeriesSetModel ComputeResourceSeriesSet(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            ResourceSettingsModel resourceSettings,
            IPresetColorSequence presetColors)
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(CoreViewModel.BuildResourceSeriesSet)}");

            return m_ResourceSchedulingService.BuildResourceSeriesSet(
                graphCompilation,
                resourceSettings,
                presetColors);
        }

        public TrackingSeriesSetModel ComputeTrackingSeriesSet(
            IList<ActivityModel> activityModels,
            ResourceSettingsModel resourceSettings,
            bool hasResources) =>
            ComputeTrackingSeriesSet(activityModels, resourceSettings, hasResources, m_BuildColors);

        public TrackingSeriesSetModel ComputeTrackingSeriesSet(
            IList<ActivityModel> activityModels,
            ResourceSettingsModel resourceSettings,
            bool hasResources,
            IPresetColorSequence presetColors)
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(CoreViewModel.BuildTrackingSeriesSet)}");

            return m_ResourceSchedulingService.BuildTrackingSeriesSet(
                activityModels,
                resourceSettings,
                hasResources,
                presetColors);
        }

        public NetworkModel ComputeNetworkMetrics(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors,
            DateTimeOffset projectStart,
            int? startTime,
            int? finishTime)
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(CoreViewModel.BuildNetworkMetrics)}");

            return m_MetricCalculationService.BuildNetworkMetrics(
                graphCompilation,
                hasCompilationErrors,
                projectStart,
                startTime,
                finishTime);
        }

        public RisksModel ComputeRiskMetrics(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors,
            IEnumerable<ActivitySeverityModel> activitySeverities)
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(CoreViewModel.BuildRiskMetrics)}");

            return m_MetricCalculationService.BuildRiskMetrics(
                graphCompilation,
                hasCompilationErrors,
                activitySeverities);
        }

        public (CostsModel Costs, BillingsModel Billings, MarginsModel Margins, EffortsModel Efforts, List<ResourceMetricsModel> ResourceMetrics) ComputeFinancialMetrics(
            ResourceSeriesSetModel resourceSeriesSet,
            bool hasCompilationErrors)
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(CoreViewModel.BuildFinancialMetrics)}");

            return m_MetricCalculationService.BuildFinancialMetrics(
                resourceSeriesSet,
                hasCompilationErrors);
        }

        #endregion
    }
}
