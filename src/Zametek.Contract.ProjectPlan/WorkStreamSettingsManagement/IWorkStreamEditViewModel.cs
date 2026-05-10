using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IWorkStreamEditViewModel
        : IDisposable
    {
        bool IsPhase { get; set; }
        bool IsIsPhaseActive { get; set; }

        ColorFormatModel ColorFormat { get; set; }
        bool IsColorFormatActive { get; set; }

        UpdateWorkStreamModel BuildUpdateModel();
    }
}
