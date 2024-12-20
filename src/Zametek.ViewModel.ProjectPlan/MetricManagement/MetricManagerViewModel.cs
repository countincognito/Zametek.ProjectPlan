using AutoMapper;
using ReactiveUI;
using System.Reactive.Linq;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.ViewModel.ProjectPlan
{
    public class MetricManagerViewModel
        : ToolViewModelBase, IMetricManagerViewModel
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly IMapper m_Mapper;

        private readonly IDisposable? m_BuildMetricsSub;
        private readonly IDisposable? m_BuildCostsAndEffortsSub;

        #endregion

        #region Ctors

        public MetricManagerViewModel(
            ICoreViewModel coreViewModel,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator,
            IMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(mapper);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_Mapper = mapper;

            m_Metrics = new MetricsModel();
            m_Costs = new CostsModel();
            m_Efforts = new EffortsModel();

            m_IsBusy = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.IsBusy)
                .ToProperty(this, mm => mm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, mm => mm.HasStaleOutputs);

            m_ShowDates = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.ShowDates)
                .ToProperty(this, mm => mm.ShowDates);

            m_HasCompilationErrors = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, mm => mm.HasCompilationErrors);

            m_BuildMetricsSub = this
                .WhenAnyValue(
                    mm => mm.m_CoreViewModel.GraphCompilation,
                    mm => mm.m_CoreViewModel.ArrowGraphSettings,
                    mm => mm.HasCompilationErrors)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildMetricsAsync());

            m_BuildCostsAndEffortsSub = this
                .WhenAnyValue(
                    mm => mm.m_CoreViewModel.ResourceSeriesSet,
                    mm => mm.HasCompilationErrors)
                .ObserveOn(RxApp.TaskpoolScheduler)
                .Subscribe(async _ => await BuildCostsAndEffortsAsync());

            m_CriticalityRisk = this
                .WhenAnyValue(mm => mm.Metrics, metrics => metrics.Criticality)
                .ToProperty(this, mm => mm.CriticalityRisk);

            m_FibonacciRisk = this
                .WhenAnyValue(mm => mm.Metrics, metrics => metrics.Fibonacci)
                .ToProperty(this, mm => mm.FibonacciRisk);

            m_ActivityRisk = this
                .WhenAnyValue(mm => mm.Metrics, metrics => metrics.Activity)
                .ToProperty(this, mm => mm.ActivityRisk);

            m_ActivityRiskWithStdDevCorrection = this
                .WhenAnyValue(mm => mm.Metrics, metrics => metrics.ActivityStdDevCorrection)
                .ToProperty(this, mm => mm.ActivityRiskWithStdDevCorrection);

            m_GeometricCriticalityRisk = this
                .WhenAnyValue(mm => mm.Metrics, metrics => metrics.GeometricCriticality)
                .ToProperty(this, mm => mm.GeometricCriticalityRisk);

            m_GeometricFibonacciRisk = this
                .WhenAnyValue(mm => mm.Metrics, metrics => metrics.GeometricFibonacci)
                .ToProperty(this, mm => mm.GeometricFibonacciRisk);

            m_GeometricActivityRisk = this
                 .WhenAnyValue(mm => mm.Metrics, metrics => metrics.GeometricActivity)
                 .ToProperty(this, mm => mm.GeometricActivityRisk);

            m_CyclomaticComplexity = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.CyclomaticComplexity)
                .ToProperty(this, mm => mm.CyclomaticComplexity);

            m_Duration = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.Duration)
                .ToProperty(this, mm => mm.Duration);

            m_DurationManMonths = this
                .WhenAnyValue(
                    mm => mm.m_CoreViewModel.Duration,
                    mm => mm.m_DateTimeCalculator.DaysPerWeek,
                    (int? duration, int daysPerWeek) =>
                        duration is null || duration == 0 || daysPerWeek == 0 ? null : duration / (daysPerWeek * 52 / 12.0))
                .ToProperty(this, mm => mm.DurationManMonths);

            m_ProjectFinish = this
                .WhenAnyValue(
                    mm => mm.m_CoreViewModel.ShowDates,
                    mm => mm.m_CoreViewModel.ProjectStart,
                    mm => mm.m_CoreViewModel.Duration,
                    mm => mm.m_DateTimeCalculator.DaysPerWeek,
                    mm => mm.m_DateTimeCalculator.CalculatorMode,
                    mm => mm.m_DateTimeCalculator.DisplayMode,
                    (bool showDates, DateTimeOffset projectStart, int? duration, int daysPerWeek, DateTimeCalculatorMode calculatorMode, DateTimeDisplayMode displayMode) =>
                    {
                        if (duration is null || duration == 0)
                        {
                            return string.Empty;
                        }

                        if (showDates)
                        {
                            int durationValue = duration.GetValueOrDefault();
                            return m_DateTimeCalculator.DisplayFinishDate(
                                m_DateTimeCalculator.AddDays(
                                    projectStart,
                                    durationValue),
                                m_DateTimeCalculator.AddDays(
                                    projectStart,
                                    durationValue),
                                1)
                            .ToString(DateTimeCalculator.DateFormat);
                        }

                        return duration.GetValueOrDefault().ToString();
                    })
                .ToProperty(this, mm => mm.ProjectFinish);

            m_DirectCost = this
                 .WhenAnyValue(mm => mm.Costs, costs => costs.Direct)
                 .ToProperty(this, mm => mm.DirectCost);

            m_IndirectCost = this
                 .WhenAnyValue(mm => mm.Costs, costs => costs.Indirect)
                 .ToProperty(this, mm => mm.IndirectCost);

            m_OtherCost = this
                 .WhenAnyValue(mm => mm.Costs, costs => costs.Other)
                 .ToProperty(this, mm => mm.OtherCost);

            m_TotalCost = this
                 .WhenAnyValue(mm => mm.Costs, costs => costs.Direct + costs.Indirect + costs.Other)
                 .ToProperty(this, mm => mm.TotalCost);

            m_DirectEffort = this
                 .WhenAnyValue(mm => mm.Efforts, efforts => efforts.Direct)
                 .ToProperty(this, mm => mm.DirectEffort);

            m_IndirectEffort = this
                 .WhenAnyValue(mm => mm.Efforts, efforts => efforts.Indirect)
                 .ToProperty(this, mm => mm.IndirectEffort);

            m_OtherEffort = this
                 .WhenAnyValue(mm => mm.Efforts, efforts => efforts.Other)
                 .ToProperty(this, mm => mm.OtherEffort);

            m_TotalEffort = this
                 .WhenAnyValue(mm => mm.Efforts, efforts => efforts.Direct + efforts.Indirect + efforts.Other)
                 .ToProperty(this, mm => mm.TotalEffort);

            m_ActivityEffort = this
                 .WhenAnyValue(mm => mm.Efforts, efforts => efforts.Activity)
                 .ToProperty(this, mm => mm.ActivityEffort);

            m_Efficiency = this
                 .WhenAnyValue(mm => mm.ActivityEffort, mm => mm.TotalEffort,
                    (double? activityEffort, double? totalEffort) => activityEffort is null || activityEffort == 0 || totalEffort == 0 ? null : activityEffort / totalEffort)
                 .ToProperty(this, mm => mm.Efficiency);

            Id = Resource.ProjectPlan.Titles.Title_Metrics;
            Title = Resource.ProjectPlan.Titles.Title_Metrics;
        }

        #endregion

        #region Properties

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates => m_ShowDates.Value;

        private MetricsModel m_Metrics;
        public MetricsModel Metrics
        {
            get => m_Metrics;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_Metrics, value);
            }
        }

        private CostsModel m_Costs;
        public CostsModel Costs
        {
            get => m_Costs;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_Costs, value);
            }
        }

        private EffortsModel m_Efforts;
        public EffortsModel Efforts
        {
            get => m_Efforts;
            set
            {
                lock (m_Lock) this.RaiseAndSetIfChanged(ref m_Efforts, value);
            }
        }

        #endregion

        #region Private Methods

        private static double CalculateCriticalityRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverityLookup);
            double numerator = activities.Sum(activity => activitySeverityLookup.FindSlackCriticalityWeight(activity.TotalSlack));
            double denominator = activitySeverityLookup.CriticalCriticalityWeight() * activities.Count();
            return denominator == 0 ? 1.0 : numerator / denominator;
        }

        private static double CalculateFibonacciRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverityLookup);
            double numerator = activities.Sum(activity => activitySeverityLookup.FindSlackFibonacciWeight(activity.TotalSlack));
            double denominator = activitySeverityLookup.CriticalFibonacciWeight() * activities.Count();
            return denominator == 0 ? 1.0 : numerator / denominator;
        }

        private static double CalculateActivityRisk(IEnumerable<ActivityModel> activities)
        {
            ArgumentNullException.ThrowIfNull(activities);
            double numerator = 0.0;
            double maxTotalSlack = 0.0;
            foreach (ActivityModel activity in activities.Where(x => x.TotalSlack.HasValue))
            {
                double totalSlack = Convert.ToDouble(activity.TotalSlack.GetValueOrDefault());
                if (totalSlack > maxTotalSlack)
                {
                    maxTotalSlack = totalSlack;
                }
                numerator += totalSlack;
            }
            double denominator = maxTotalSlack * activities.Count();
            return denominator == 0 ? 1.0 : 1.0 - (numerator / denominator);
        }

        private static double CalculateActivityRiskWithStdDevCorrection(IEnumerable<ActivityModel> activities)
        {
            ArgumentNullException.ThrowIfNull(activities);
            double numerator = 0.0;
            double maxTotalSlack = 0.0;

            IList<double> totalSlacks = activities
                .Where(x => x.TotalSlack.HasValue)
                .Select(x => Convert.ToDouble(x.TotalSlack.GetValueOrDefault()))
                .ToList();

            double correctionValue = 0;
            if (totalSlacks.Count > 0)
            {
                double meanAverage = totalSlacks.Average();
                double sumOfSquaresOfDifferences = totalSlacks.Select(val => (val - meanAverage) * (val - meanAverage)).Sum();
                double stdDev = Math.Sqrt(sumOfSquaresOfDifferences / totalSlacks.Count);
                correctionValue = Math.Round(meanAverage + stdDev, MidpointRounding.AwayFromZero);
            }

            foreach (double totalSlack in totalSlacks)
            {
                double localTotalSlack = totalSlack;
                if (localTotalSlack > correctionValue)
                {
                    localTotalSlack = correctionValue;
                }
                if (localTotalSlack > maxTotalSlack)
                {
                    maxTotalSlack = localTotalSlack;
                }
                numerator += localTotalSlack;
            }
            double denominator = maxTotalSlack * activities.Count();
            return denominator == 0 ? 1.0 : 1.0 - (numerator / denominator);
        }

        private static double CalculateGeometricCriticalityRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverityLookup);
            double numerator = 1.0;
            foreach (ActivityModel activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackCriticalityWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            double denominator = activitySeverityLookup.CriticalCriticalityWeight();
            return denominator == 0 ? 1.0 : numerator / denominator;
        }

        private static double CalculateGeometricFibonacciRisk(
            IEnumerable<ActivityModel> activities,
            ActivitySeverityLookup activitySeverityLookup)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverityLookup);
            double numerator = 1.0;
            foreach (ActivityModel activity in activities)
            {
                numerator *= activitySeverityLookup.FindSlackFibonacciWeight(activity.TotalSlack);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            double denominator = activitySeverityLookup.CriticalFibonacciWeight();
            return denominator == 0 ? 1.0 : numerator / denominator;
        }

        private static double CalculateGeometricActivityRisk(IEnumerable<ActivityModel> activities)
        {
            ArgumentNullException.ThrowIfNull(activities);
            double numerator = 1.0;
            double maxTotalSlack = 0.0;
            foreach (ActivityModel activity in activities.Where(x => x.TotalSlack.HasValue))
            {
                double totalSlack = Convert.ToDouble(activity.TotalSlack.GetValueOrDefault());
                if (totalSlack > maxTotalSlack)
                {
                    maxTotalSlack = totalSlack;
                }
                numerator *= (totalSlack + 1.0);
            }
            numerator = Math.Pow(numerator, 1.0 / activities.Count());
            numerator -= 1.0;
            double denominator = maxTotalSlack;
            return denominator == 0 ? 1.0 : 1.0 - (numerator / denominator);
        }

        private static MetricsModel CalculateProjectMetrics(
            IEnumerable<ActivityModel> activities,
            IEnumerable<ActivitySeverityModel> activitySeverities)
        {
            ArgumentNullException.ThrowIfNull(activities);
            ArgumentNullException.ThrowIfNull(activitySeverities);
            var activitySeverityLookup = new ActivitySeverityLookup(activitySeverities);
            return new MetricsModel
            {
                Criticality = CalculateCriticalityRisk(activities, activitySeverityLookup),
                Fibonacci = CalculateFibonacciRisk(activities, activitySeverityLookup),
                Activity = CalculateActivityRisk(activities),
                ActivityStdDevCorrection = CalculateActivityRiskWithStdDevCorrection(activities),
                GeometricCriticality = CalculateGeometricCriticalityRisk(activities, activitySeverityLookup),
                GeometricFibonacci = CalculateGeometricFibonacciRisk(activities, activitySeverityLookup),
                GeometricActivity = CalculateGeometricActivityRisk(activities),
            };
        }

        private async Task BuildMetricsAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    BuildMetrics();
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        private static CostsModel CalculateProjectCosts(IList<ResourceSeriesModel> resourceSeriesModels)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModels);

            return new CostsModel
            {
                Direct = resourceSeriesModels
                    .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Direct)
                    .Sum(static x =>
                    {
                        double accumulator(bool y) => y ? x.UnitCost : 0.0;
                        return x.ResourceSchedule.ActivityAllocation.Sum(accumulator);
                    }),
                Indirect = resourceSeriesModels
                    .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect)
                    .Sum(static x =>
                    {
                        double accumulator(bool y) => y ? x.UnitCost : 0.0;
                        return x.ResourceSchedule.ActivityAllocation.Sum(accumulator);
                    }),
                Other = resourceSeriesModels
                    .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.None)
                    .Sum(static x =>
                    {
                        double accumulator(bool y) => y ? x.UnitCost : 0.0;
                        return x.ResourceSchedule.ActivityAllocation.Sum(accumulator);
                    })
            };
        }

        private static EffortsModel CalculateProjectEfforts(IList<ResourceSeriesModel> resourceSeriesModels)
        {
            ArgumentNullException.ThrowIfNull(resourceSeriesModels);
            static double allocationAccumulator(bool x) => x ? 1.0 : 0.0;
            static int durationAccumulator(ScheduledActivityModel x) => x.Duration;

            return new EffortsModel
            {
                Direct = resourceSeriesModels
                    .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Direct)
                    .Sum(static x => x.ResourceSchedule.ActivityAllocation.Sum(allocationAccumulator)),
                Indirect = resourceSeriesModels
                    .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.Indirect)
                    .Sum(static x => x.ResourceSchedule.ActivityAllocation.Sum(allocationAccumulator)),
                Other = resourceSeriesModels
                    .Where(static x => x.InterActivityAllocationType == InterActivityAllocationType.None)
                    .Sum(static x => x.ResourceSchedule.ActivityAllocation.Sum(allocationAccumulator)),
                Activity = resourceSeriesModels
                    .Sum(static x => x.ResourceSchedule.ScheduledActivities.Sum(durationAccumulator))
            };
        }

        private async Task BuildCostsAndEffortsAsync()
        {
            try
            {
                lock (m_Lock)
                {
                    BuildCostsAndEfforts();
                }
            }
            catch (Exception ex)
            {
                await m_DialogService.ShowErrorAsync(
                    Resource.ProjectPlan.Titles.Title_Error,
                    string.Empty,
                    ex.Message);
            }
        }

        #endregion

        #region IMetricManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<double?> m_CriticalityRisk;
        public double? CriticalityRisk => m_CriticalityRisk.Value;

        private readonly ObservableAsPropertyHelper<double?> m_FibonacciRisk;
        public double? FibonacciRisk => m_FibonacciRisk.Value;

        private readonly ObservableAsPropertyHelper<double?> m_ActivityRisk;
        public double? ActivityRisk => m_ActivityRisk.Value;

        private readonly ObservableAsPropertyHelper<double?> m_ActivityRiskWithStdDevCorrection;
        public double? ActivityRiskWithStdDevCorrection => m_ActivityRiskWithStdDevCorrection.Value;

        private readonly ObservableAsPropertyHelper<double?> m_GeometricCriticalityRisk;
        public double? GeometricCriticalityRisk => m_GeometricCriticalityRisk.Value;

        private readonly ObservableAsPropertyHelper<double?> m_GeometricFibonacciRisk;
        public double? GeometricFibonacciRisk => m_GeometricFibonacciRisk.Value;

        private readonly ObservableAsPropertyHelper<double?> m_GeometricActivityRisk;
        public double? GeometricActivityRisk => m_GeometricActivityRisk.Value;

        private readonly ObservableAsPropertyHelper<int?> m_CyclomaticComplexity;
        public int? CyclomaticComplexity => m_CyclomaticComplexity.Value;

        private readonly ObservableAsPropertyHelper<int?> m_Duration;
        public int? Duration => m_Duration.Value;

        private readonly ObservableAsPropertyHelper<double?> m_DurationManMonths;
        public double? DurationManMonths => m_DurationManMonths.Value;

        private readonly ObservableAsPropertyHelper<string> m_ProjectFinish;
        public string ProjectFinish => m_ProjectFinish.Value;

        private readonly ObservableAsPropertyHelper<double?> m_DirectCost;
        public double? DirectCost => m_DirectCost.Value;

        private readonly ObservableAsPropertyHelper<double?> m_IndirectCost;
        public double? IndirectCost => m_IndirectCost.Value;

        private readonly ObservableAsPropertyHelper<double?> m_OtherCost;
        public double? OtherCost => m_OtherCost.Value;

        private readonly ObservableAsPropertyHelper<double?> m_TotalCost;
        public double? TotalCost => m_TotalCost.Value;

        private readonly ObservableAsPropertyHelper<double?> m_DirectEffort;
        public double? DirectEffort => m_DirectEffort.Value;

        private readonly ObservableAsPropertyHelper<double?> m_IndirectEffort;
        public double? IndirectEffort => m_IndirectEffort.Value;

        private readonly ObservableAsPropertyHelper<double?> m_OtherEffort;
        public double? OtherEffort => m_OtherEffort.Value;

        private readonly ObservableAsPropertyHelper<double?> m_TotalEffort;
        public double? TotalEffort => m_TotalEffort.Value;

        private readonly ObservableAsPropertyHelper<double?> m_ActivityEffort;
        public double? ActivityEffort => m_ActivityEffort.Value;

        private readonly ObservableAsPropertyHelper<double?> m_Efficiency;
        public double? Efficiency => m_Efficiency.Value;

        public void BuildMetrics()
        {
            var metricsModel = new MetricsModel();

            lock (m_Lock)
            {
                IEnumerable<IDependentActivity> dependentActivities =
                    m_CoreViewModel.GraphCompilation.DependentActivities.Select(x => (IDependentActivity)x.CloneObject());

                if (dependentActivities.Any())
                {
                    if (!HasCompilationErrors)
                    {
                        IEnumerable<ActivityModel> activities =
                            m_Mapper.Map<IEnumerable<IActivity<int, int, int>>, IList<ActivityModel>>(
                                dependentActivities.Where(x => !x.IsDummy).Select(x => (IActivity<int, int, int>)x));

                        IEnumerable<ActivitySeverityModel> activitySeverities = m_CoreViewModel.ArrowGraphSettings.ActivitySeverities;

                        metricsModel = CalculateProjectMetrics(activities, activitySeverities);
                    }
                }
            }

            Metrics = metricsModel;
        }

        public void BuildCostsAndEfforts()
        {
            var costsModel = new CostsModel();
            var effortsModel = new EffortsModel();

            lock (m_Lock)
            {
                IList<ResourceSeriesModel> combinedResourceSeriesModels = m_CoreViewModel.ResourceSeriesSet.Combined;

                if (combinedResourceSeriesModels.Any())
                {
                    if (!HasCompilationErrors)
                    {
                        costsModel = CalculateProjectCosts(combinedResourceSeriesModels);
                        effortsModel = CalculateProjectEfforts(combinedResourceSeriesModels);
                    }
                }
            }

            Costs = costsModel;
            Efforts = effortsModel;
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
            m_BuildMetricsSub?.Dispose();
            m_BuildCostsAndEffortsSub?.Dispose();
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                KillSubscriptions();
                m_IsBusy?.Dispose();
                m_HasStaleOutputs?.Dispose();
                m_ShowDates?.Dispose();
                m_HasCompilationErrors?.Dispose();
                m_CriticalityRisk?.Dispose();
                m_FibonacciRisk?.Dispose();
                m_ActivityRisk?.Dispose();
                m_ActivityRiskWithStdDevCorrection?.Dispose();
                m_GeometricCriticalityRisk?.Dispose();
                m_GeometricFibonacciRisk?.Dispose();
                m_GeometricActivityRisk?.Dispose();
                m_CyclomaticComplexity?.Dispose();
                m_Duration?.Dispose();
                m_DurationManMonths?.Dispose();
                m_ProjectFinish?.Dispose();
                m_DirectCost?.Dispose();
                m_IndirectCost?.Dispose();
                m_OtherCost?.Dispose();
                m_TotalCost?.Dispose();
                m_DirectEffort?.Dispose();
                m_IndirectEffort?.Dispose();
                m_OtherEffort?.Dispose();
                m_TotalEffort?.Dispose();
                m_ActivityEffort?.Dispose();
                m_Efficiency?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
