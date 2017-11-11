using Prism.Events;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class CoreViewModel
        : PropertyChangedPubSubViewModel
    {
        #region Fields

        private string m_CompilationOutput;

        #endregion

        #region Ctors

        public CoreViewModel(IEventAggregator eventService)
            : base(eventService)
        { }

        #endregion

        #region Properties

        public string CompilationOutput
        {
            get
            {
                return m_CompilationOutput;
            }
            set
            {
                m_CompilationOutput = value;
                RaisePropertyChanged();
            }
        }

        #endregion
    }
}
