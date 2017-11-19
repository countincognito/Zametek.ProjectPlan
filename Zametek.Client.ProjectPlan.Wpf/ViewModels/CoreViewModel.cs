using Prism.Events;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Zametek.Common.Project;
using Zametek.Common.ProjectPlan;
using Zametek.Maths.Graphs;

namespace Zametek.Client.ProjectPlan.Wpf
{
    public class CoreViewModel
        : PropertyChangedPubSubViewModel, ICoreViewModel
    {
        #region Fields

        private string m_CompilationOutput;
        private bool m_HasCompilationErrors;

        #endregion

        #region Ctors

        public CoreViewModel(IEventAggregator eventService)
            : base(eventService)
        {
            Activities = new ObservableCollection<ManagedActivityViewModel>();
            ResourceDtos = new List<ResourceDto>();
        }

        #endregion

        #region ICoreViewModel Members

        public ObservableCollection<ManagedActivityViewModel> Activities
        {
            get;
        }

        public bool DisableResources
        {
            get;
            set;
        }

        public IList<ResourceDto> ResourceDtos
        {
            get;
        }

        public MetricsDto MetricsDto
        {
            get;
            set;
        }











        public GraphCompilation<int, IDependentActivity<int>> GraphCompilation
        {
            get;
            set;
        }

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

        public bool HasCompilationErrors
        {
            get
            {
                return m_HasCompilationErrors;
            }
            set
            {
                m_HasCompilationErrors = value;
                RaisePropertyChanged();
            }
        }

        #endregion
    }
}
