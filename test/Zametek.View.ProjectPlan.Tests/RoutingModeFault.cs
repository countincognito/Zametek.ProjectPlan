using System.ComponentModel;
using Zametek.Contract.ProjectPlan;

namespace Zametek.View.ProjectPlan.Tests
{
    /// <summary>
    /// Makes one change of the routing mode of the arrow graph throw. The exception goes through the display settings into the
    /// load that made the change, as the exception of the graph did when a load reached its edges on the wrong thread.
    /// </summary>
    /// <remarks>
    /// A load that changes the mode changes it twice: the reset puts back the default, and the settings of the scenario put
    /// back its own mode. Thus, the first change fails in the reset, and the second fails after it.
    /// </remarks>
    internal sealed class RoutingModeFault
        : IDisposable
    {
        private readonly INotifyPropertyChanged m_DisplaySettings;
        private readonly int m_FailingChange;
        private int m_Changes;

        public RoutingModeFault(ICoreViewModel core, int failingChange)
        {
            m_DisplaySettings = (INotifyPropertyChanged)core.DisplaySettingsViewModel;
            m_FailingChange = failingChange;
            m_DisplaySettings.PropertyChanged += OnPropertyChanged;
        }

        private void OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IProjectScenarioDisplaySettingsViewModel.ArrowGraphEdgeRoutingMode)
                && ++m_Changes == m_FailingChange)
            {
                throw new InvalidOperationException(@"Collection was modified; enumeration operation may not execute.");
            }
        }

        public void Dispose() => m_DisplaySettings.PropertyChanged -= OnPropertyChanged;
    }
}
