using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedResourceViewModel
    {
        int Id { get; }

        ResourceModel Resource { get; }
    }
}
