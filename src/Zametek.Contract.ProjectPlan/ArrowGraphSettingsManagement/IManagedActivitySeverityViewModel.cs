using System.ComponentModel;
using Zametek.Common.ProjectPlan;

namespace Zametek.Contract.ProjectPlan
{
    public interface IManagedActivitySeverityViewModel
        : IDisposable, INotifyPropertyChanged
    {
        Guid Id { get; }

        int SlackLimit { get; set; }

        double CriticalityWeight { get; set; }

        double FibonacciWeight { get; set; }

        ColorFormatModel ColorFormat { get; set; }
    }
}
