using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class MetricManagerViewModel
        : ToolViewModelBase, IMetricManagerViewModel
    {
        #region Fields

        private readonly Lock m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IDialogService m_DialogService;
        private readonly IDateTimeCalculator m_DateTimeCalculator;
        private readonly ProjectPlanMapper m_Mapper;

        #endregion

        #region Ctors

        public MetricManagerViewModel(
            ICoreViewModel coreViewModel,
            IDialogService dialogService,
            IDateTimeCalculator dateTimeCalculator,
            ProjectPlanMapper mapper)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(dialogService);
            ArgumentNullException.ThrowIfNull(dateTimeCalculator);
            ArgumentNullException.ThrowIfNull(mapper);
            m_Lock = new();
            m_CoreViewModel = coreViewModel;
            m_DialogService = dialogService;
            m_DateTimeCalculator = dateTimeCalculator;
            m_Mapper = mapper;

            m_IsBusy = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.IsBusy)
                .ToProperty(this, mm => mm.IsBusy);

            m_HasStaleOutputs = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, mm => mm.HasStaleOutputs);

            m_ShowDates = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.DisplaySettingsViewModel.ShowDates)
                .ToProperty(this, mm => mm.ShowDates);

            m_HasCompilationErrors = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.HasCompilationErrors)
                .ToProperty(this, mm => mm.HasCompilationErrors);

            m_HideCost = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.DisplaySettingsViewModel.HideCost)
                .ToProperty(this, mm => mm.HideCost);

            m_HideBilling = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.DisplaySettingsViewModel.HideBilling)
                .ToProperty(this, mm => mm.HideBilling);

            m_HideMargin = this
                .WhenAnyValue(
                    mm => mm.HideCost,
                    mm => mm.HideBilling,
                    (hideCost, hideBilling) => hideCost || hideBilling)
                .ToProperty(this, mm => mm.HideMargin);

            m_RisksMetrics = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.Metrics, metrics => metrics.Risks)
                .ToProperty(this, mm => mm.RisksMetrics);

            m_CostsMetrics = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.Metrics, metrics => metrics.Costs)
                .ToProperty(this, mm => mm.CostsMetrics);

            m_BillingsMetrics = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.Metrics, metrics => metrics.Billings)
                .ToProperty(this, mm => mm.BillingsMetrics);

            m_MarginsMetrics = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.Metrics, metrics => metrics.Margins)
                .ToProperty(this, mm => mm.MarginsMetrics);

            m_EffortsMetrics = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.Metrics, metrics => metrics.Efforts)
                .ToProperty(this, mm => mm.EffortsMetrics);

            m_NetworkMetrics = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.Metrics, metrics => metrics.Network)
                .ToProperty(this, mm => mm.NetworkMetrics);

            m_CriticalityRisk = this
                .WhenAnyValue(mm => mm.RisksMetrics, risks => risks.Criticality)
                .ToProperty(this, mm => mm.CriticalityRisk);

            m_FibonacciRisk = this
                .WhenAnyValue(mm => mm.RisksMetrics, risks => risks.Fibonacci)
                .ToProperty(this, mm => mm.FibonacciRisk);

            m_ActivityRisk = this
                .WhenAnyValue(mm => mm.RisksMetrics, risks => risks.Activity)
                .ToProperty(this, mm => mm.ActivityRisk);

            m_ActivityRiskWithStdDevCorrection = this
                .WhenAnyValue(mm => mm.RisksMetrics, risks => risks.ActivityStdDevCorrection)
                .ToProperty(this, mm => mm.ActivityRiskWithStdDevCorrection);

            m_GeometricCriticalityRisk = this
                .WhenAnyValue(mm => mm.RisksMetrics, risks => risks.GeometricCriticality)
                .ToProperty(this, mm => mm.GeometricCriticalityRisk);

            m_GeometricFibonacciRisk = this
                .WhenAnyValue(mm => mm.RisksMetrics, risks => risks.GeometricFibonacci)
                .ToProperty(this, mm => mm.GeometricFibonacciRisk);

            m_GeometricActivityRisk = this
                .WhenAnyValue(mm => mm.RisksMetrics, risks => risks.GeometricActivity)
                .ToProperty(this, mm => mm.GeometricActivityRisk);

            m_DirectCost = this
                .WhenAnyValue(mm => mm.CostsMetrics, costs => costs.Direct)
                .ToProperty(this, mm => mm.DirectCost);

            m_IndirectCost = this
                .WhenAnyValue(mm => mm.CostsMetrics, costs => costs.Indirect)
                .ToProperty(this, mm => mm.IndirectCost);

            m_OtherCost = this
                .WhenAnyValue(mm => mm.CostsMetrics, costs => costs.Other)
                .ToProperty(this, mm => mm.OtherCost);

            m_TotalCost = this
                .WhenAnyValue(mm => mm.CostsMetrics, costs => costs.Total)
                .ToProperty(this, mm => mm.TotalCost);

            m_DirectBilling = this
                .WhenAnyValue(mm => mm.BillingsMetrics, billings => billings.Direct)
                .ToProperty(this, mm => mm.DirectBilling);

            m_IndirectBilling = this
                .WhenAnyValue(mm => mm.BillingsMetrics, billings => billings.Indirect)
                .ToProperty(this, mm => mm.IndirectBilling);

            m_OtherBilling = this
                .WhenAnyValue(mm => mm.BillingsMetrics, billings => billings.Other)
                .ToProperty(this, mm => mm.OtherBilling);

            m_TotalBilling = this
                .WhenAnyValue(mm => mm.BillingsMetrics, billings => billings.Total)
                .ToProperty(this, mm => mm.TotalBilling);

            m_DirectMargin = this
                 .WhenAnyValue(mm => mm.MarginsMetrics, margins => margins.Direct)
                 .ToProperty(this, mm => mm.DirectMargin);

            m_IndirectMargin = this
                 .WhenAnyValue(mm => mm.MarginsMetrics, margins => margins.Indirect)
                 .ToProperty(this, mm => mm.IndirectMargin);

            m_OtherMargin = this
                 .WhenAnyValue(mm => mm.MarginsMetrics, margins => margins.Other)
                 .ToProperty(this, mm => mm.OtherMargin);

            m_TotalMargin = this
                 .WhenAnyValue(mm => mm.MarginsMetrics, margins => margins.Total)
                 .ToProperty(this, mm => mm.TotalMargin);

            m_DisplayDirectMargin = this
                .WhenAnyValue(
                    mm => mm.DirectMargin,
                    (double? margin) => margin is null ? string.Empty : string.Format(" ({0:P1})", margin))
                .ToProperty(this, mm => mm.DisplayDirectMargin);

            m_DisplayIndirectMargin = this
                .WhenAnyValue(
                    mm => mm.IndirectMargin,
                    (double? margin) => margin is null ? string.Empty : string.Format(" ({0:P1})", margin))
                .ToProperty(this, mm => mm.DisplayIndirectMargin);

            m_DisplayOtherMargin = this
                .WhenAnyValue(
                    mm => mm.OtherMargin,
                    (double? margin) => margin is null ? string.Empty : string.Format(" ({0:P1})", margin))
                 .ToProperty(this, mm => mm.DisplayOtherMargin);

            m_DisplayTotalMargin = this
                .WhenAnyValue(
                    mm => mm.TotalMargin,
                    (double? margin) => margin is null ? string.Empty : string.Format(" ({0:P1})", margin))
                .ToProperty(this, mm => mm.DisplayTotalMargin);

            m_DirectMarginAbsolute = this
                .WhenAnyValue(mm => mm.MarginsMetrics, margins => margins.DirectAbsolute)
                .ToProperty(this, mm => mm.DirectMarginAbsolute);

            m_IndirectMarginAbsolute = this
                .WhenAnyValue(mm => mm.MarginsMetrics, margins => margins.IndirectAbsolute)
                .ToProperty(this, mm => mm.IndirectMarginAbsolute);

            m_OtherMarginAbsolute = this
                .WhenAnyValue(mm => mm.MarginsMetrics, margins => margins.OtherAbsolute)
                .ToProperty(this, mm => mm.OtherMarginAbsolute);

            m_TotalMarginAbsolute = this
                .WhenAnyValue(mm => mm.MarginsMetrics, margins => margins.TotalAbsolute)
                .ToProperty(this, mm => mm.TotalMarginAbsolute);

            m_DirectEffort = this
                .WhenAnyValue(mm => mm.EffortsMetrics, efforts => efforts.Direct)
                .ToProperty(this, mm => mm.DirectEffort);

            m_IndirectEffort = this
                .WhenAnyValue(mm => mm.EffortsMetrics, efforts => efforts.Indirect)
                .ToProperty(this, mm => mm.IndirectEffort);

            m_OtherEffort = this
                .WhenAnyValue(mm => mm.EffortsMetrics, efforts => efforts.Other)
                .ToProperty(this, mm => mm.OtherEffort);

            m_TotalEffort = this
                .WhenAnyValue(mm => mm.EffortsMetrics, efforts => efforts.Total)
                .ToProperty(this, mm => mm.TotalEffort);

            m_ActivityEffort = this
                .WhenAnyValue(mm => mm.EffortsMetrics, efforts => efforts.Activity)
                .ToProperty(this, mm => mm.ActivityEffort);

            m_EffortEfficiency = this
                .WhenAnyValue(mm => mm.EffortsMetrics, efforts => efforts.Efficiency)
                .ToProperty(this, mm => mm.EffortEfficiency);

            m_NetworkCyclomaticComplexity = this
                .WhenAnyValue(mm => mm.NetworkMetrics, network => network.CyclomaticComplexity)
                .ToProperty(this, mm => mm.NetworkCyclomaticComplexity);

            m_NetworkDuration = this
                .WhenAnyValue(mm => mm.NetworkMetrics, network => network.Duration)
                .ToProperty(this, mm => mm.NetworkDuration);

            m_NetworkDurationManMonths = this
                .WhenAnyValue(mm => mm.NetworkMetrics, network => network.DurationManMonths)
                .ToProperty(this, mm => mm.NetworkDurationManMonths);

            m_ProjectFinish = this
                .WhenAnyValue(mm => mm.m_CoreViewModel.ProjectFinish)
                .ToProperty(this, mm => mm.ProjectFinish);

            Id = Resource.ProjectPlan.Titles.Title_Metrics;
            Title = Resource.ProjectPlan.Titles.Title_Metrics;
        }

        #endregion

        #region Properties

        private readonly ObservableAsPropertyHelper<bool> m_ShowDates;
        public bool ShowDates => m_ShowDates.Value;

        private readonly ObservableAsPropertyHelper<RisksModel> m_RisksMetrics;
        public RisksModel RisksMetrics => m_RisksMetrics.Value;

        private readonly ObservableAsPropertyHelper<CostsModel> m_CostsMetrics;
        public CostsModel CostsMetrics => m_CostsMetrics.Value;

        private readonly ObservableAsPropertyHelper<BillingsModel> m_BillingsMetrics;
        public BillingsModel BillingsMetrics => m_BillingsMetrics.Value;

        private readonly ObservableAsPropertyHelper<MarginsModel> m_MarginsMetrics;
        public MarginsModel MarginsMetrics => m_MarginsMetrics.Value;

        private readonly ObservableAsPropertyHelper<EffortsModel> m_EffortsMetrics;
        public EffortsModel EffortsMetrics => m_EffortsMetrics.Value;

        private readonly ObservableAsPropertyHelper<NetworkModel> m_NetworkMetrics;
        public NetworkModel NetworkMetrics => m_NetworkMetrics.Value;

        #endregion

        #region IMetricManagerViewModel Members

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasCompilationErrors;
        public bool HasCompilationErrors => m_HasCompilationErrors.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HideCost;
        public bool HideCost => m_HideCost.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HideBilling;
        public bool HideBilling => m_HideBilling.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HideMargin;
        public bool HideMargin => m_HideMargin.Value;

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

        private readonly ObservableAsPropertyHelper<int?> m_NetworkCyclomaticComplexity;
        public int? NetworkCyclomaticComplexity => m_NetworkCyclomaticComplexity.Value;

        private readonly ObservableAsPropertyHelper<int?> m_NetworkDuration;
        public int? NetworkDuration => m_NetworkDuration.Value;

        private readonly ObservableAsPropertyHelper<double?> m_NetworkDurationManMonths;
        public double? NetworkDurationManMonths => m_NetworkDurationManMonths.Value;

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

        private readonly ObservableAsPropertyHelper<double?> m_DirectBilling;
        public double? DirectBilling => m_DirectBilling.Value;

        private readonly ObservableAsPropertyHelper<double?> m_IndirectBilling;
        public double? IndirectBilling => m_IndirectBilling.Value;

        private readonly ObservableAsPropertyHelper<double?> m_OtherBilling;
        public double? OtherBilling => m_OtherBilling.Value;

        private readonly ObservableAsPropertyHelper<double?> m_TotalBilling;
        public double? TotalBilling => m_TotalBilling.Value;

        private readonly ObservableAsPropertyHelper<double?> m_DirectMargin;
        public double? DirectMargin => m_DirectMargin.Value;

        private readonly ObservableAsPropertyHelper<double?> m_IndirectMargin;
        public double? IndirectMargin => m_IndirectMargin.Value;

        private readonly ObservableAsPropertyHelper<double?> m_OtherMargin;
        public double? OtherMargin => m_OtherMargin.Value;

        private readonly ObservableAsPropertyHelper<double?> m_TotalMargin;
        public double? TotalMargin => m_TotalMargin.Value;

        private readonly ObservableAsPropertyHelper<string> m_DisplayDirectMargin;
        public string DisplayDirectMargin => m_DisplayDirectMargin.Value;

        private readonly ObservableAsPropertyHelper<string> m_DisplayIndirectMargin;
        public string DisplayIndirectMargin => m_DisplayIndirectMargin.Value;

        private readonly ObservableAsPropertyHelper<string> m_DisplayOtherMargin;
        public string DisplayOtherMargin => m_DisplayOtherMargin.Value;

        private readonly ObservableAsPropertyHelper<string> m_DisplayTotalMargin;
        public string DisplayTotalMargin => m_DisplayTotalMargin.Value;

        private readonly ObservableAsPropertyHelper<double?> m_DirectMarginAbsolute;
        public double? DirectMarginAbsolute => m_DirectMarginAbsolute.Value;

        private readonly ObservableAsPropertyHelper<double?> m_IndirectMarginAbsolute;
        public double? IndirectMarginAbsolute => m_IndirectMarginAbsolute.Value;

        private readonly ObservableAsPropertyHelper<double?> m_OtherMarginAbsolute;
        public double? OtherMarginAbsolute => m_OtherMarginAbsolute.Value;

        private readonly ObservableAsPropertyHelper<double?> m_TotalMarginAbsolute;
        public double? TotalMarginAbsolute => m_TotalMarginAbsolute.Value;

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

        private readonly ObservableAsPropertyHelper<double?> m_EffortEfficiency;
        public double? EffortEfficiency => m_EffortEfficiency.Value;

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
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
                m_NetworkCyclomaticComplexity?.Dispose();
                m_NetworkDuration?.Dispose();
                m_NetworkDurationManMonths?.Dispose();
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
                m_EffortEfficiency?.Dispose();
            }

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
