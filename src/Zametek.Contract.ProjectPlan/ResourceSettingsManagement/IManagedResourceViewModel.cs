using System.ComponentModel;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedResourceViewModel
        : IResource<int>, IDisposable, INotifyPropertyChanged
    {
        int DisplayOrder { get; set; }

        ColorFormatModel ColorFormat { get; set; }
    }
}
