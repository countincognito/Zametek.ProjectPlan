using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IWorkStreamEditViewModel
        : IDisposable
    {
        bool IsPhase { get; set; }
        bool IsIsPhaseActive { get; set; }

        bool IsHiddenFromCharts { get; set; }
        bool IsIsHiddenFromChartsActive { get; set; }

        ColorFormatModel ColorFormat { get; set; }
        bool IsColorFormatActive { get; set; }

        UpdateWorkStreamModel BuildUpdateModel();
    }
}
