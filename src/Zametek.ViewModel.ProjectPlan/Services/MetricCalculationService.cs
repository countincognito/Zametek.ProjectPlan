using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class MetricCalculationService
        : IMetricCalculationService
    {
        #region Fields

        private readonly ProjectPlanMapper m_Mapper;
        private readonly IDateTimeCalculator m_DateTimeCalculator;

        #endregion

        #region Ctors

        public MetricCalculationService(
            ProjectPlanMapper mapper,
            IDateTimeCalculator dateTimeCalculator)
        {
            ArgumentNullException.ThrowIfNull(mapper);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            m_Mapper = mapper;
            m_DateTimeCalculator = dateTimeCalculator;
        }

        #endregion

        #region Private Methods

        private static int? CalculateCyclomaticComplexity(IEnumerable<IDependentActivity> dependentActivities)
        {
            ArgumentNullException.ThrowIfNull(dependentActivities);

            IEnumerable<IDependentActivity> dependentActivitiesCopy =
                dependentActivities.Select(x => (IDependentActivity)x.CloneObject());

            if (!dependentActivitiesCopy.Any())
            {
                return null;
            }

            var vertexGraphCompiler = new VertexGraphCompiler();

            foreach (var dependentActivity in dependentActivitiesCopy.Cast<DependentActivity>())
            {
                dependentActivity.Dependencies.UnionWith(dependentActivity.PlanningDependencies);
                dependentActivity.Dependencies.UnionWith(dependentActivity.ResourceDependencies);
                dependentActivity.PlanningDependencies.Clear();
                dependentActivity.ResourceDependencies.Clear();
                vertexGraphCompiler.AddActivity(dependentActivity);
            }

            vertexGraphCompiler.TransitiveReduction();
            return vertexGraphCompiler.CyclomaticComplexity;
        }

        private double? CalculateDurationManMonths(DateTimeOffset projectStart, int? duration)
        {
            (_, DateTimeOffset? endDate) = m_DateTimeCalculator.CalculateTimeAndDateTime(projectStart, duration);

            if (endDate is not null)
            {
                TimeSpan diff = endDate.Value - projectStart;
                double days = diff.TotalDays;
                return 12.0 * (days / 365.0);
            }
            return null;
        }

        #endregion

        #region IMetricCalculationService Members

        public NetworkModel BuildNetworkMetrics(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors,
            DateTimeOffset projectStart,
            int? startTime,
            int? finishTime)
        {
            ArgumentNullException.ThrowIfNull(graphCompilation);

            int? cyclomaticComplexity = null;
            int? duration = null;
            double? durationManMonths = null;

            if (!hasCompilationErrors)
            {
                if (graphCompilation.DependentActivities.Any())
                {
                    cyclomaticComplexity = CalculateCyclomaticComplexity(graphCompilation.DependentActivities);
                    duration = finishTime - startTime;
                    durationManMonths = CalculateDurationManMonths(projectStart, duration);
                }
            }

            return new NetworkModel
            {
                CyclomaticComplexity = cyclomaticComplexity,
                Duration = duration,
                DurationManMonths = durationManMonths,
            };
        }

        public RisksModel BuildRiskMetrics(
            IGraphCompilation<int, int, int, IDependentActivity> graphCompilation,
            bool hasCompilationErrors,
            IEnumerable<ActivitySeverityModel> activitySeverities)
        {
            ArgumentNullException.ThrowIfNull(graphCompilation);
            ArgumentNullException.ThrowIfNull(activitySeverities);

            var risksModel = new RisksModel();

            List<IDependentActivity> dependentActivities =
                [.. graphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject())];

            if (dependentActivities.Count != 0)
            {
                if (!hasCompilationErrors)
                {
                    IEnumerable<ActivityModel> activities = dependentActivities
                        .Where(x => !x.IsDummy)
                        .Cast<DependentActivity<int, int, int>>()
                        .Select(m_Mapper.ToActivityModel);

                    risksModel = MetricsHelper.CalculateProjectRisks(activities, activitySeverities);
                }
            }

            return risksModel;
        }

        public (CostsModel costs, BillingsModel billings, MarginsModel margins, EffortsModel efforts)
            BuildFinancialMetrics(
            ResourceSeriesSetModel resourceSeriesSet,
            bool hasCompilationErrors)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesSet);

            var costsModel = new CostsModel();
            var billingsModel = new BillingsModel();
            var marginsModel = new MarginsModel();
            var effortsModel = new EffortsModel();

            List<ResourceSeriesModel> combinedResourceSeriesModels = resourceSeriesSet.Combined;

            if (combinedResourceSeriesModels.Count != 0)
            {
                if (!hasCompilationErrors)
                {
                    costsModel = MetricsHelper.CalculateProjectCosts(combinedResourceSeriesModels);
                    billingsModel = MetricsHelper.CalculateProjectBillings(combinedResourceSeriesModels);
                    marginsModel = MetricsHelper.CalculateProjectMargins(costsModel, billingsModel);
                    effortsModel = MetricsHelper.CalculateProjectEfforts(combinedResourceSeriesModels);
                }
            }

            return (costsModel, billingsModel, marginsModel, effortsModel);
        }

        #endregion
    }
}
