using System.ComponentModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedWorkStreamViewModel
        : IDisposable, INotifyPropertyChanged
    {
        int Id { get; }

        string Name { get; set; }

        bool IsPhase { get; set; }

        int DisplayOrder { get; set; }

        ColorFormatModel ColorFormat { get; set; }
    }
}
