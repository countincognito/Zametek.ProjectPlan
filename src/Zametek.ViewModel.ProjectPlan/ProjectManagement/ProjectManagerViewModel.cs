using ReactiveUI;
using Zametek.Common.ProjectPlan;
using Zametek.Contract.ProjectPlan;

namespace Zametek.ViewModel.ProjectPlan
{
    public class ProjectManagerViewModel
        : ToolViewModelBase, IProjectManagerViewModel, IDisposable
    {
        #region Fields

        private readonly object m_Lock;

        private readonly ICoreViewModel m_CoreViewModel;
        private readonly IDialogService m_DialogService;

        #endregion

        #region Ctors

        public ProjectManagerViewModel(
            ICoreViewModel coreViewModel,
            IDialogService dialogService)
        {
            ArgumentNullException.ThrowIfNull(coreViewModel);
            ArgumentNullException.ThrowIfNull(dialogService);
            m_Lock = new object();
            m_CoreViewModel = coreViewModel;
            m_DialogService = dialogService;

            m_ProjectTitle = this
                .WhenAnyValue(
                    pm => pm.m_CoreViewModel.ProjectTitle)
                .ToProperty(this, pm => pm.ProjectTitle);

            m_IsBusy = this
                .WhenAnyValue(pm => pm.m_CoreViewModel.IsBusy)
                .ToProperty(this, pm => pm.IsBusy);

            m_IsProjectUpdated = this
                .WhenAnyValue(pm => pm.m_CoreViewModel.IsProjectUpdated)
                .ToProperty(this, pm => pm.IsProjectUpdated);

            m_HasStaleOutputs = this
                .WhenAnyValue(pm => pm.m_CoreViewModel.HasStaleOutputs)
                .ToProperty(this, pm => pm.HasStaleOutputs);


            Id = Resource.ProjectPlan.Titles.Title_Project;
            Title = Resource.ProjectPlan.Titles.Title_Project;
        }

        #endregion

        #region Properties

        #endregion

        #region Private Methods

        #endregion

        #region IOutputManagerViewModel

        private readonly ObservableAsPropertyHelper<string> m_ProjectTitle;
        public string ProjectTitle => m_ProjectTitle.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsBusy;
        public bool IsBusy => m_IsBusy.Value;

        private readonly ObservableAsPropertyHelper<bool> m_IsProjectUpdated;
        public bool IsProjectUpdated => m_IsProjectUpdated.Value;

        private readonly ObservableAsPropertyHelper<bool> m_HasStaleOutputs;
        public bool HasStaleOutputs => m_HasStaleOutputs.Value;




        public void ResetProject()
        {
        }


        public void ProcessProject(ProjectModel projectModel)
        {
            //string output = string.Empty;

            //lock (m_Lock)
            //{
            //    output = BuildCompilationOutputInternal(
            //        m_DateTimeCalculator,
            //        m_CoreViewModel.GraphCompilation,
            //        m_CoreViewModel.ResourceSeriesSet,
            //        ShowDates,
            //        ProjectStart,
            //        HasCompilationErrors);
            //}

            //CompilationOutput = output;
        }

        public ProjectModel BuildProject()
        {
            return null;
        }

        #endregion

        #region IKillSubscriptions Members

        public void KillSubscriptions()
        {
        }

        #endregion

        #region IDisposable Members

        private bool m_Disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (m_Disposed)
            {
                return;
            }

            if (disposing)
            {
                // TODO: dispose managed state (managed objects).
                KillSubscriptions();
                m_ProjectTitle?.Dispose();
                m_IsBusy?.Dispose();
                m_IsProjectUpdated?.Dispose();
                m_HasStaleOutputs?.Dispose();
            }

            // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
            // TODO: set large fields to null.

            m_Disposed = true;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
