using Avalonia.Controls;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Threading.Tasks;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan
{
    public partial class MetricManagerView
        : UserControl
    {
        public MetricManagerView()
        {
            InitializeComponent();
        }

        private async void CopyTable_Click(object? sender, RoutedEventArgs e)
        {
            await CopyTableToClipboardAsync();
        }

        // The metrics panel is label/value pairs rather than a DataGrid, so the
        // copied "table" is a two-column Metric|Value list built from the
        // view-model in panel block order, with the hidden (cost/billing/margin)
        // blocks omitted as rows. Values are raw, so each margin line splits
        // into an absolute row and a ratio row. The panel's labels are form
        // labels with trailing colons, which the copy trims.
        private async Task CopyTableToClipboardAsync()
        {
            if (DataContext is not IMetricManagerViewModel vm)
            {
                return;
            }

            List<IReadOnlyList<object?>> rows = [];

            void AddRow(string label, object? value) =>
                rows.Add([label.TrimEnd(':', ' '), value]);

            AddRow(Resource.ProjectPlan.Labels.Label_ActivityRisk, vm.ActivityRisk);
            AddRow(Resource.ProjectPlan.Labels.Label_ActivityRiskWithStdDevCorrection, vm.ActivityRiskWithStdDevCorrection);
            AddRow(Resource.ProjectPlan.Labels.Label_CriticalityRisk, vm.CriticalityRisk);
            AddRow(Resource.ProjectPlan.Labels.Label_FibonacciRisk, vm.FibonacciRisk);

            AddRow(Resource.ProjectPlan.Labels.Label_CyclomaticComplexity, vm.NetworkCyclomaticComplexity);
            AddRow(Resource.ProjectPlan.Labels.Label_GeometricActivityRisk, vm.GeometricActivityRisk);
            AddRow(Resource.ProjectPlan.Labels.Label_GeometricCriticalityRisk, vm.GeometricCriticalityRisk);
            AddRow(Resource.ProjectPlan.Labels.Label_GeometricFibonacciRisk, vm.GeometricFibonacciRisk);

            AddRow(Resource.ProjectPlan.Labels.Label_ActivityEffort, vm.ActivityEffort);
            AddRow(Resource.ProjectPlan.Labels.Label_DurationManMonths, vm.NetworkDurationManMonths);
            AddRow(Resource.ProjectPlan.Labels.Label_ProjectFinish, vm.ProjectFinish);
            AddRow(Resource.ProjectPlan.Labels.Label_EffortEfficiency, vm.EffortEfficiency);

            AddRow(Resource.ProjectPlan.Labels.Label_DirectEffort, vm.DirectEffort);
            AddRow(Resource.ProjectPlan.Labels.Label_IndirectEffort, vm.IndirectEffort);
            AddRow(Resource.ProjectPlan.Labels.Label_OtherEffort, vm.OtherEffort);
            AddRow(Resource.ProjectPlan.Labels.Label_TotalEffort, vm.TotalEffort);

            if (!vm.HideCost)
            {
                AddRow(Resource.ProjectPlan.Labels.Label_DirectCost, vm.DirectCost);
                AddRow(Resource.ProjectPlan.Labels.Label_IndirectCost, vm.IndirectCost);
                AddRow(Resource.ProjectPlan.Labels.Label_OtherCost, vm.OtherCost);
                AddRow(Resource.ProjectPlan.Labels.Label_TotalCost, vm.TotalCost);
            }

            if (!vm.HideBilling)
            {
                AddRow(Resource.ProjectPlan.Labels.Label_DirectBilling, vm.DirectBilling);
                AddRow(Resource.ProjectPlan.Labels.Label_IndirectBilling, vm.IndirectBilling);
                AddRow(Resource.ProjectPlan.Labels.Label_OtherBilling, vm.OtherBilling);
                AddRow(Resource.ProjectPlan.Labels.Label_TotalBilling, vm.TotalBilling);
            }

            if (!vm.HideMargin)
            {
                AddRow(Resource.ProjectPlan.Labels.Label_DirectMargin, vm.DirectMarginAbsolute);
                AddRow(Resource.ProjectPlan.Labels.Label_DirectMarginPercent, vm.DirectMargin);
                AddRow(Resource.ProjectPlan.Labels.Label_IndirectMargin, vm.IndirectMarginAbsolute);
                AddRow(Resource.ProjectPlan.Labels.Label_IndirectMarginPercent, vm.IndirectMargin);
                AddRow(Resource.ProjectPlan.Labels.Label_OtherMargin, vm.OtherMarginAbsolute);
                AddRow(Resource.ProjectPlan.Labels.Label_OtherMarginPercent, vm.OtherMargin);
                AddRow(Resource.ProjectPlan.Labels.Label_TotalMargin, vm.TotalMarginAbsolute);
                AddRow(Resource.ProjectPlan.Labels.Label_TotalMarginPercent, vm.TotalMargin);
            }

            await DataGridHelper.CopyTableToClipboardAsync(
                this,
                [Resource.ProjectPlan.Labels.Label_Metric, Resource.ProjectPlan.Labels.Label_Value],
                rows,
                vm.ReportErrorAsync);
        }
    }
}
