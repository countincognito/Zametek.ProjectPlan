using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    /// <summary>
    /// Builds the outputs of a compilation: the two graphs, the two series sets and the metrics. Each build reads
    /// only what it is given, and so it needs no lock of the core. One lock of its own keeps two builds from drawing
    /// on the colour sequence of the resource scheduling service at the same time.
    /// </summary>
    internal sealed class CompilationOutputService
    {
        #region Fields

        /// <summary>
        /// Held while the outputs of a cascade are built, so that two builds never draw from the colour sequence of
        /// the resource scheduling service at the same time. A cascade builds without holding the lock of the core,
        /// and a bulk update builds one while it holds it.
        /// </summary>
        /// <remarks>
        /// A leaf lock: nothing that holds it takes the lock of the core, and it is taken only by the builds. The
        /// resource series and the tracking series share one sequence, and the second carries on where the first
        /// stopped, so they have to be built one after the other and as a pair.
        /// </remarks>
        private readonly Lock m_OutputBuildLock;

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
            m_OutputBuildLock = new();
            m_GraphCompilationService = graphCompilationService;
            m_ResourceSchedulingService = resourceSchedulingService;
            m_MetricCalculationService = metricCalculationService;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Builds the outputs of a cascade, in dependency order, from a copy of what they read. Needs no lock
        /// of the core, and takes <see cref="m_OutputBuildLock"/> for the colours.
        /// </summary>
        public CascadeOutputs Compute(CascadeInputs inputs)
        {
            lock (m_OutputBuildLock)
            {
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
                    inputs.ResourceSettings);

                TrackingSeriesSetModel trackingSeriesSet = ComputeTrackingSeriesSet(
                    inputs.ActivityModels,
                    inputs.ResourceSettings,
                    inputs.HasResources);

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

        public ResourceSeriesSetModel ComputeResourceSeriesSet(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            ResourceSettingsModel resourceSettings)
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(CoreViewModel.BuildResourceSeriesSet)}");

            lock (m_OutputBuildLock)
            {
                return m_ResourceSchedulingService.BuildResourceSeriesSet(
                    graphCompilation,
                    resourceSettings);
            }
        }

        public TrackingSeriesSetModel ComputeTrackingSeriesSet(
            IList<ActivityModel> activityModels,
            ResourceSettingsModel resourceSettings,
            bool hasResources)
        {
            CascadeDiagnostics.RecordBuild($@"{nameof(CoreViewModel)}.{nameof(CoreViewModel.BuildTrackingSeriesSet)}");

            lock (m_OutputBuildLock)
            {
                return m_ResourceSchedulingService.BuildTrackingSeriesSet(
                    activityModels,
                    resourceSettings,
                    hasResources);
            }
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
